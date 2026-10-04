# Derive power-simulation inputs for the complexity GLMM from the pilot study.
#
# What the pilot can and cannot supply:
#   - base rate and lane effect per outcome       -> from a null GLMM on the pilot chains
#   - config variance (39 configs)                -> from the same fit, usable
#   - between-candidate variance                  -> from the same fit, but the pilot has
#     one candidate per repo, so repo and candidate variance are confounded, and 4
#     levels make the SD barely identified. Treat it as a centre for a grid, not a value.
#   - complexity_index transform                  -> fit once on the MSR method corpus (all
#     non-test methods) and frozen: centering, scaling, sign and PC1 weights.
#   - complexity_index distribution               -> NOT from the pilot (4 methods); the
#     frozen transform applied to MSR experiment candidates only (the default selector's
#     picks from `python -m analysis candidate-paths`), so the simulated predictor looks
#     like the evaluation's. The simulation samples repo-then-candidate.
#   - the complexity effect                       -> never from the pilot; it is the SESOI.
#
# Run from Analysis/:
#   Rscript R/pilot_parameters.R
#   Rscript R/pilot_parameters.R --candidates=<evaluation_candidates.csv> --index-method=zmean
#   Rscript R/pilot_parameters.R --candidate-filter=      # all eligible, not only default picks

get_script_dir <- function() {
  file <- sub("^--file=", "", grep("^--file=", commandArgs(FALSE), value = TRUE))
  if (length(file)) dirname(normalizePath(file)) else file.path(getwd(), "R")
}
SCRIPT_DIR <- get_script_dir()
ANALYSIS_ROOT <- dirname(SCRIPT_DIR)
source(file.path(SCRIPT_DIR, "common.R"))
source(file.path(SCRIPT_DIR, "power_helpers.R"))

suppressPackageStartupMessages({
  library(lme4)
  library(jsonlite)
})

opts <- parse_args(list(
  candidates = file.path(dirname(ANALYSIS_ROOT), "Replication", "PilotStudy", "evaluation",
                         "evaluation_candidates.csv"),
  msr_frames = file.path(ANALYSIS_ROOT, "msr-validation-data", "frames"),
  candidate_paths = file.path(SCRIPT_DIR, "output", "msr_candidate_paths.csv"),
  candidate_filter = "selected_default",
  index_method = "pc1",
  min_sloc = 1,
  out = file.path(SCRIPT_DIR, "output")
))
dir.create(opts$out, recursive = TRUE, showWarnings = FALSE)

# ---- Outcome model inputs: one row per chain (candidate x config) -------------------
# A config is (lane, producer, budget arm): the chain grain in normalize.py. Repair
# steps inside a chain are not independent evidence, so the GLMM unit is the chain.
chains <- read.csv(opts$candidates, stringsAsFactors = FALSE)
chains$config <- paste(chains$lane, chains$producer, chains$budget_mode, sep = "|")
chains$lane <- factor(chains$lane, levels = c("llm", "agentic"))
outcomes <- chain_outcomes(chains)
chains$success <- outcomes$success
chains$vep <- outcomes$vep

fit_null <- function(outcome) {
  d <- chains[!is.na(chains[[outcome]]), ]
  d$y <- d[[outcome]]
  model <- glmer(y ~ lane + (1 | candidate_key) + (1 | config), data = d, family = binomial,
                 control = glmerControl(optimizer = "bobyqa"))
  vc <- as.data.frame(VarCorr(model))
  n_candidates <- length(unique(d$candidate_key))
  n_repos <- length(unique(d$repository_key))
  list(
    outcome = outcome,
    n_chains = nrow(d),
    n_candidates = n_candidates,
    n_repos = n_repos,
    n_configs = length(unique(d$config)),
    intercept = unname(fixef(model)["(Intercept)"]),
    lane_effect = unname(fixef(model)["laneagentic"]),
    sd_candidate_total = vc$sdcor[vc$grp == "candidate_key"],
    sd_candidate_includes_repo = n_candidates == n_repos,
    sd_config = vc$sdcor[vc$grp == "config"],
    singular = isSingular(model),
    base_rate = as.list(tapply(d$y, d$lane, mean))
  )
}
outcome_params <- lapply(c(success = "success", vep = "vep"), fit_null)
agentic_share <- mean(tapply(as.character(chains$lane), chains$config, `[`, 1) == "agentic")

# ---- complexity_index: transform frozen on the MSR method corpus ---------------------
entities <- read.csv(file.path(opts$msr_frames, "msr_entities.csv"), stringsAsFactors = FALSE,
                     colClasses = c(name = "character"))
methods <- entities[entities$entity_kind == "member" & entities$kind == "method" &
                      entities$is_test == 0 & entities$parent_is_test == 0,
                    c("repo_key", "entity_id")]
metrics <- read.csv(file.path(opts$msr_frames, "msr_code_metrics.csv"), stringsAsFactors = FALSE)
metrics <- metrics[metrics$entity_kind == "member", ]
corpus <- merge(methods, metrics, by = c("repo_key", "entity_id"))
corpus <- corpus[!is.na(corpus$source_lines_of_code) & corpus$source_lines_of_code >= opts$min_sloc, ]

transform <- fit_complexity_transform(corpus, opts$index_method)
transform$corpus <- list(n_methods = nrow(corpus), n_repos = length(unique(corpus$repo_key)),
                         min_sloc = opts$min_sloc)
transform_file <- file.path(opts$out, "complexity_transform.json")
write_json(transform[setdiff(names(transform), "correlation")], transform_file,
           auto_unbox = TRUE, pretty = TRUE, digits = NA)
write.csv(round(transform$correlation, 3), file.path(opts$out, "complexity_metric_correlation.csv"))

# ---- complexity_index distribution: experiment candidates only -----------------------
candidates <- read.csv(opts$candidate_paths, stringsAsFactors = FALSE)
if (nzchar(opts$candidate_filter)) candidates <- candidates[candidates[[opts$candidate_filter]] == 1, ]
candidates <- candidates[stats::complete.cases(candidates[unlist(transform$metrics)]), ]
candidates$complexity_index <- scale_complexity_index(apply_complexity_transform(candidates, transform))

icc_model <- lmer(complexity_index ~ 1 + (1 | repo_key), data = candidates)
icc_vc <- as.data.frame(VarCorr(icc_model))$vcov
complexity_icc <- icc_vc[1] / sum(icc_vc)

pool_file <- file.path(opts$out, "complexity_pool.csv")
write.csv(candidates[c("repo_key", "complexity_index")], pool_file, row.names = FALSE)

params <- list(
  source = list(candidates = normalizePath(opts$candidates), msr_frames = normalizePath(opts$msr_frames),
                candidate_paths = normalizePath(opts$candidate_paths),
                generated = format(Sys.time(), "%Y-%m-%dT%H:%M:%S")),
  agentic_share = agentic_share,
  outcomes = outcome_params,
  complexity = list(
    pool_file = normalizePath(pool_file),
    transform_file = normalizePath(transform_file),
    method = transform$method,
    candidate_filter = opts$candidate_filter,
    n_candidates = nrow(candidates),
    n_repos = length(unique(candidates$repo_key)),
    pc1_share = transform$pc1_share,
    loadings = transform$loadings,
    dropped_metrics = transform$dropped,
    repo_icc = complexity_icc
  )
)
params_file <- file.path(opts$out, "pilot_parameters.json")
write_json(params, params_file, auto_unbox = TRUE, pretty = TRUE, digits = NA)

# ---- Console summary ------------------------------------------------------------------
for (p in outcome_params) {
  cat(sprintf(
    "%-8s chains=%d candidates=%d repos=%d configs=%d | intercept(llm)=%.2f lane=%.2f | sd_candidate=%.2f%s sd_config=%.2f%s | rate llm=%.2f agentic=%.2f\n",
    p$outcome, p$n_chains, p$n_candidates, p$n_repos, p$n_configs, p$intercept, p$lane_effect,
    p$sd_candidate_total, if (p$sd_candidate_includes_repo) " (repo+candidate)" else "",
    p$sd_config, if (p$singular) " SINGULAR" else "",
    p$base_rate$llm, p$base_rate$agentic))
}
cat(sprintf("configs: agentic share %.2f\n", agentic_share))
cat(sprintf("complexity transform (%s) frozen on %d corpus methods in %d repos: PC1 share %.2f%s\n",
            transform$method, nrow(corpus), length(unique(corpus$repo_key)), transform$pc1_share,
            if (length(transform$dropped)) paste0(", dropped zero-variance: ", paste(transform$dropped, collapse = ", ")) else ""))
cat("PC1 loadings:", paste(sprintf("%s=%.2f", names(transform$loadings), unlist(transform$loadings)), collapse = " "), "\n")
cat(sprintf("simulation pool: %d candidates (%s) in %d repos, repo ICC %.2f\n",
            nrow(candidates), if (nzchar(opts$candidate_filter)) opts$candidate_filter else "all eligible",
            length(unique(candidates$repo_key)), complexity_icc))
cat("wrote", params_file, "\n")
