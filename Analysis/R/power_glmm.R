# Simulation-based power analysis for a candidate-level predictor GLMM (Kumle et al., 2021).
#
#   y ~ <predictor> + lane + (1 | repo/candidate) + (1 | config), family = binomial
#
# For each grid cell: build a design (repos x candidates x configs), draw the predictor
# from an MSR pool (repo first, then candidates within the repo), fix the model
# parameters with simr::makeGlmer, and let simr::powerSim simulate/refit `nsim` times,
# counting Wald-z p < alpha on the predictor. Odds ratio 1 is the type I error check.
#
# Predictors:
#   complexity_index (default) -- pool from pilot_parameters.R, z-scored, OR per SD.
#   path_hops_capped           -- pool from `python -m analysis candidate-paths`, hops from
#                                 the paired test to the candidate (1, 2, 3+), OR per hop.
#                                 --pool-filter=selected_default keeps only the candidates the
#                                 default (non-randomized) selector would pick.
#
# The effect is a smallest effect size of interest, not a pilot estimate. Base rate,
# lane effect and config SD default to the pilot values in pilot_parameters.json; the
# between-candidate SD is barely identified by the pilot, so it is a grid.
#
# Cells are written to <out>/cells/ as they finish and are skipped on rerun, so an
# interrupted run resumes. Cache identity includes exact settings, input/code hashes,
# and runtime versions; incompatible or old cache entries are not reused.
#
# Run from Analysis/ (after R/pilot_parameters.R):
#   Rscript R/power_glmm.R --nsim=20                                  # smoke test
#   Rscript R/power_glmm.R                                            # complexity, 1000 sims
#   Rscript R/power_glmm.R --outcomes=vep --n-repos=30,40 --cand-per-repo=6,8,10
#   Rscript R/power_glmm.R --predictor=path_hops_capped --pool=R/output/msr_candidate_paths.csv #       --pool-filter=selected_default --out=R/output/power_path
#   Rscript R/power_glmm.R --help

get_script_dir <- function() {
  file <- sub("^--file=", "", grep("^--file=", commandArgs(FALSE), value = TRUE))
  if (length(file)) dirname(normalizePath(file)) else file.path(getwd(), "R")
}
SCRIPT_DIR <- get_script_dir()
source(file.path(SCRIPT_DIR, "common.R"))
source(file.path(SCRIPT_DIR, "power_helpers.R"))

suppressPackageStartupMessages({
  library(lme4)
  library(simr)
  library(jsonlite)
  library(parallel)
})

opts <- parse_args(list(
  params = file.path(SCRIPT_DIR, "output", "pilot_parameters.json"),
  predictor = "complexity_index",   # model term, and pool column unless --pool-column is set
  pool = "",                        # CSV with repo_key + predictor; "": complexity pool
  pool_column = "",
  pool_filter = "",                 # keep rows where this 0/1 column is 1
  standardize = NA,                 # NA: z-score complexity_index only
  outcomes = c("success", "vep"),
  odds_ratios = c(1.5, 1.3, 1),     # per predictor unit (SD if standardized); 1 = type I check
  effect_direction = -1,            # -1: higher predictor -> lower odds of success
  n_repos = 34,
  cand_per_repo = 12,
  n_configs = 39,
  agentic_share = NA_real_,         # NA: pilot share of agentic configs
  sd_candidate = c(0.5, 1, 2),      # total between-candidate SD (repo + candidate), logit
  repo_share = 0.5,                 # share of that variance at the repo level
  sd_config = NA_real_,             # NA: pilot value per outcome
  intercept = NA_real_,             # NA: pilot value per outcome (LLM lane, logit)
  lane_effect = NA_real_,           # NA: pilot value per outcome (agentic vs LLM, logit)
  nsim = 1000,
  design_draws = 10,                # designs per cell; nsim is split across them
  alpha = 0.05,
  test = "z",                       # "z" (Wald) or "lr" (likelihood ratio, slower)
  fast = FALSE,                     # nAGQ = 0 refits: quicker, for exploring a grid
  workers = max(1, parallel::detectCores() - 2),
  seed = 20260918,
  out = file.path(SCRIPT_DIR, "output", "power")
))

params <- fromJSON(opts$params, simplifyVector = FALSE)
cells_dir <- file.path(opts$out, "cells")
dir.create(cells_dir, recursive = TRUE, showWarnings = FALSE)

pool_file <- if (nzchar(opts$pool)) opts$pool else params$complexity$pool_file
pool_column <- if (nzchar(opts$pool_column)) opts$pool_column else opts$predictor
pool <- read.csv(pool_file, stringsAsFactors = FALSE)
if (nzchar(opts$pool_filter)) pool <- pool[pool[[opts$pool_filter]] == 1, ]
if (!pool_column %in% names(pool)) stop("Pool ", pool_file, " has no column '", pool_column, "'")
pool <- pool[!is.na(pool[[pool_column]]), ]
pool_by_repo <- split(pool[[pool_column]], pool$repo_key)
opts$standardize <- if (is.na(opts$standardize)) opts$predictor == "complexity_index" else opts$standardize
pool_tag <- paste0(tools::file_path_sans_ext(basename(pool_file)),
                   if (nzchar(opts$pool_filter)) paste0("-", opts$pool_filter) else "")
cat(sprintf("predictor %s from %s: %d candidates in %d repos (%d with >= %d)
",
            opts$predictor, pool_tag, nrow(pool), length(pool_by_repo),
            sum(lengths(pool_by_repo) >= max(opts$cand_per_repo)), max(opts$cand_per_repo)))
agentic_share <- if (is.na(opts$agentic_share)) params$agentic_share else opts$agentic_share

pilot_or <- function(value, outcome, field) {
  if (length(value) == 1 && is.na(value)) params$outcomes[[outcome]][[field]] else value
}

# ---- Grid ------------------------------------------------------------------------------
grid <- do.call(rbind, lapply(opts$outcomes, function(outcome) {
  if (is.null(params$outcomes[[outcome]])) stop("Outcome '", outcome, "' not in ", opts$params)
  expand.grid(
    outcome = outcome,
    odds_ratio = opts$odds_ratios,
    n_repos = opts$n_repos,
    cand_per_repo = opts$cand_per_repo,
    n_configs = opts$n_configs,
    sd_candidate_total = opts$sd_candidate,
    repo_share = opts$repo_share,
    sd_config = pilot_or(opts$sd_config, outcome, "sd_config"),
    intercept = pilot_or(opts$intercept, outcome, "intercept"),
    lane_effect = pilot_or(opts$lane_effect, outcome, "lane_effect"),
    stringsAsFactors = FALSE
  )
}))
grid$beta <- opts$effect_direction * log(grid$odds_ratio)
grid$sd_repo <- sqrt(grid$repo_share) * grid$sd_candidate_total
grid$sd_candidate <- sqrt(1 - grid$repo_share) * grid$sd_candidate_total
provenance <- list(
  inputs = unname(tools::md5sum(c(pool_file, opts$params))),
  code = tools::md5sum(file.path(SCRIPT_DIR, c("power_glmm.R", "common.R", "power_helpers.R"))),
  R = R.version.string, platform = R.version$platform, rng = RNGkind(),
  packages = vapply(c("lme4", "simr", "Matrix", "minqa", "nloptr", "jsonlite"),
                    function(p) as.character(packageVersion(p)), character(1))
)
specs <- lapply(seq_len(nrow(grid)), function(i)
  power_cache_spec(grid[i, ], opts, agentic_share, pool_column, provenance))
grid$cell_id <- vapply(specs, power_cache_id, character(1))
names(specs) <- grid$cell_id
# Repeated identical CLI values must not dispatch concurrent writers for one cell.
grid <- grid[!duplicated(grid$cell_id), , drop = FALSE]
specs <- specs[grid$cell_id]

# ---- One cell ----------------------------------------------------------------------------
build_design <- function(cell, pool_by_repo, agentic_share, seed, predictor, standardize) {
  set.seed(seed)
  eligible <- names(pool_by_repo)[lengths(pool_by_repo) >= cell$cand_per_repo]
  repos <- eligible[sample.int(length(eligible), cell$n_repos,
                               replace = length(eligible) < cell$n_repos)]
  candidates <- do.call(rbind, lapply(seq_along(repos), function(i) {
    methods <- pool_by_repo[[repos[i]]]
    data.frame(
      repo = sprintf("r%03d", i),
      candidate = sprintf("r%03d_c%02d", i, seq_len(cell$cand_per_repo)),
      value = methods[sample.int(length(methods), cell$cand_per_repo)]
    )
  }))
  # The analysis z-scores over its own candidates, so the simulation does too.
  if (standardize) candidates$value <- as.numeric(scale(candidates$value))
  names(candidates)[names(candidates) == "value"] <- predictor

  n_agentic <- round(cell$n_configs * agentic_share)
  configs <- data.frame(
    config = sprintf("cfg%02d", seq_len(cell$n_configs)),
    lane = rep(c("agentic", "llm"), c(n_agentic, cell$n_configs - n_agentic))
  )
  design <- merge(candidates, configs, by = NULL)
  design$repo <- factor(design$repo)
  design$candidate <- factor(design$candidate)
  design$config <- factor(design$config)
  design$lane <- factor(design$lane, levels = c("llm", "agentic"))
  design
}

run_cell <- function(cell, pool_by_repo, agentic_share, opts) {
  started <- Sys.time()
  formula <- glmm_formula(opts$predictor)
  sd_by_group <- c("candidate:repo" = cell$sd_candidate, repo = cell$sd_repo, config = cell$sd_config)
  settings <- power_fit_settings(opts$fast)
  fit_opts <- list(control = lme4::glmerControl(optimizer = settings$optimizer,
                                              calc.derivs = settings$calc.derivs),
                   nAGQ = settings$nAGQ)
  sims_with <- function(log, pattern) {
    if (is.null(log) || !nrow(log)) return(0L)
    length(unique(log$index[grepl(pattern, log$message, ignore.case = TRUE)]))
  }

  # Which repos and candidates the study gets is itself random, so power averages over
  # `design_draws` designs. Design seeds depend only on the design size, so every cell of
  # the same size sees the same designs and cells differ only in their parameters.
  draws <- max(1, min(opts$design_draws, opts$nsim))
  nsim_per_draw <- diff(round(seq(0, opts$nsim, length.out = draws + 1)))
  design_seed <- opts$seed + cell$n_repos * 1e4 + cell$cand_per_repo * 100 + cell$n_configs
  design_results <- vector("list", draws)
  for (d in seq_len(draws)) {
    design <- build_design(cell, pool_by_repo, agentic_share, design_seed + 7919 * d,
                           opts$predictor, opts$standardize)

    # makeGlmer takes variance components in lme4's random-effect term order; read that
    # order from the model frame instead of assuming it.
    design$y <- rbinom(nrow(design), 1, 0.5)
    groups <- names(lme4::glFormula(formula, data = design, family = binomial)$reTrms$cnms)
    if (!setequal(groups, names(sd_by_group))) stop("Unexpected random-effect terms: ", paste(groups, collapse = ", "))
    design$y <- NULL

    model <- simr::makeGlmer(
      formula, family = binomial,
      fixef = c(cell$intercept, cell$beta, cell$lane_effect),
      VarCorr = as.list(unname(sd_by_group[groups])^2),
      data = design
    )
    stopifnot(names(lme4::fixef(model)) == c("(Intercept)", opts$predictor, "laneagentic"))

    sim <- simr::powerSim(model, test = simr::fixed(opts$predictor, opts$test),
                          nsim = nsim_per_draw[d], alpha = opts$alpha,
                          seed = design_seed + 7919 * d + 100000000,
                          fitOpts = fit_opts, progress = FALSE)
    design_results[[d]] <- data.frame(
      cell_id = cell$cell_id, draw = d,
      design_seed = design_seed + 7919 * d,
      simulation_seed = design_seed + 7919 * d + 100000000,
      trials = nsim_per_draw[d], successes = summary(sim)$successes,
      fit_errors = if (is.null(sim$errors)) 0L else length(unique(sim$errors$index)),
      singular_fits = sims_with(rbind(sim$warnings, sim$messages), "singular"),
      convergence_warnings = sims_with(sim$warnings, "converge"),
      predictor_sd = sd(design[[opts$predictor]][!duplicated(design$candidate)]),
      marginal_rate = mean(stats::simulate(model, nsim = 1)[[1]])
    )
  }
  design_results <- do.call(rbind, design_results)
  estimate <- summarize_design_power(design_results$successes, design_results$trials)

  result <- cbind(
    predictor = opts$predictor,
    pool = opts$pool_tag,
    standardized = opts$standardize,
    design_draws = draws,
    predictor_sd = mean(design_results$predictor_sd),
    predictor_sd_min = min(design_results$predictor_sd),
    predictor_sd_max = max(design_results$predictor_sd),
    cell[setdiff(names(cell), "row")],
    n_candidates = cell$n_repos * cell$cand_per_repo,
    n_rows = cell$n_repos * cell$cand_per_repo * cell$n_configs,
    nsim = opts$nsim,
    alpha = opts$alpha,
    test = opts$test,
    fast = opts$fast,
    calc_derivs = settings$calc.derivs,
    nAGQ = settings$nAGQ,
    seed = opts$seed,
    agentic_share = agentic_share,
    successes = sum(design_results$successes),
    as.data.frame(estimate),
    fit_errors = sum(design_results$fit_errors),
    singular_fits = sum(design_results$singular_fits),
    convergence_warnings = sum(design_results$convergence_warnings),
    marginal_rate = mean(design_results$marginal_rate),
    elapsed_min = as.numeric(difftime(Sys.time(), started, units = "mins"))
  )
  saveRDS(list(spec = opts$specs[[cell$cell_id]], result = result, designs = design_results),
          file.path(opts$cells_dir, paste0(cell$cell_id, ".rds")))
  result
}

# ---- Run ---------------------------------------------------------------------------------
opts$cells_dir <- cells_dir
opts$pool_tag <- pool_tag
opts$specs <- specs
cache_paths <- file.path(cells_dir, paste0(grid$cell_id, ".rds"))
done <- vapply(seq_len(nrow(grid)), function(i)
  !is.null(read_power_cache(cache_paths[i], specs[[i]])), logical(1))
todo <- split(grid[!done, ], seq_len(sum(!done)))
cat(sprintf("%d cells (%d cached, %d to run), nsim=%d, %d workers\n",
            nrow(grid), sum(done), length(todo), opts$nsim, min(opts$workers, max(1, length(todo)))))

if (length(todo)) {
  if (opts$workers > 1 && length(todo) > 1) {
    cl <- makeCluster(min(opts$workers, length(todo)))
    clusterEvalQ(cl, suppressPackageStartupMessages({ library(lme4); library(simr) }))
    clusterExport(cl, c("build_design", "glmm_formula", "power_fit_settings", "summarize_design_power"))
    tryCatch(
      parLapplyLB(cl, todo, run_cell, pool_by_repo = pool_by_repo,
                  agentic_share = agentic_share, opts = opts),
      finally = stopCluster(cl)
    )
  } else {
    for (cell in todo) {
      r <- run_cell(cell, pool_by_repo, agentic_share, opts)
      cat(sprintf("  %s  power=%.3f  (%.1f min)\n", cell$cell_id, r$power, r$elapsed_min))
    }
  }
}

cached <- lapply(seq_len(nrow(grid)), function(i) read_power_cache(cache_paths[i], specs[[i]]))
if (any(vapply(cached, is.null, logical(1)))) stop("Missing or incompatible power cell after execution")
results <- do.call(rbind, lapply(cached, `[[`, "result"))
write.csv(do.call(rbind, lapply(cached, `[[`, "designs")),
          file.path(opts$out, "power_design_results.csv"), row.names = FALSE)
results <- results[order(results$outcome, -results$odds_ratio, results$sd_candidate_total,
                         results$n_repos, results$cand_per_repo), ]
results_file <- file.path(opts$out, "power_results.csv")
write.csv(results, results_file, row.names = FALSE)

shown <- results[c("outcome", "odds_ratio", "n_repos", "cand_per_repo", "sd_candidate_total",
                   "marginal_rate", "power", "ci_lower", "ci_upper", "fit_errors", "singular_fits")]
shown[c("marginal_rate", "power", "ci_lower", "ci_upper")] <-
  round(shown[c("marginal_rate", "power", "ci_lower", "ci_upper")], 3)
print(shown, row.names = FALSE)

type1 <- results[results$odds_ratio == 1, ]
if (nrow(type1)) {
  off <- type1$ci_lower > opts$alpha | type1$ci_upper < opts$alpha
  if (any(off, na.rm = TRUE)) cat(sprintf("WARNING: %d type I cells exclude alpha=%.2f from their CI\n", sum(off, na.rm = TRUE), opts$alpha))
}
if (anyNA(results$ci_lower)) cat("WARNING: power uncertainty needs at least two independent design draws.\n")
cat("wrote", results_file, "\n")
