# Pure helpers for power analysis; sourceable without GLMM packages or simulations.

power_fit_settings <- function(fast) {
  stopifnot(is.logical(fast), length(fast) == 1L, !is.na(fast))
  list(optimizer = "bobyqa", calc.derivs = !fast, nAGQ = if (fast) 0L else 1L)
}

power_cache_spec <- function(cell, opts, agentic_share, pool_column, provenance) {
  # Grid row numbers and presentation labels are not scientific inputs. Including
  # the exact cell values avoids the collisions caused by rounded filenames.
  cell <- as.list(cell[setdiff(names(cell), c("row", "cell_id"))])
  settings <- opts[c("predictor", "standardize", "pool_filter", "nsim", "design_draws",
                     "alpha", "test", "fast", "seed", "effect_direction")]
  list(version = 2L, cell = cell, settings = settings, agentic_share = agentic_share,
       pool_column = pool_column, fit = power_fit_settings(opts$fast), provenance = provenance)
}

power_cache_id <- function(spec) {
  # Base R hashing avoids adding a package dependency. The serialized object keeps
  # full numeric precision; MD5 is used for cache identity, not security.
  path <- tempfile("power-spec-", fileext = ".rds")
  on.exit(unlink(path))
  saveRDS(spec, path, version = 2, compress = FALSE)
  paste0("v2_", unname(tools::md5sum(path)))
}

read_power_cache <- function(path, spec) {
  if (!file.exists(path)) return(NULL)
  cached <- tryCatch(readRDS(path), error = function(e) NULL)
  if (!is.list(cached) || !identical(cached$spec, spec) ||
      !is.data.frame(cached$result) || nrow(cached$result) != 1L ||
      !is.data.frame(cached$designs) || !nrow(cached$designs)) return(NULL)
  cached
}

summarize_design_power <- function(successes, trials, level = .95) {
  stopifnot(length(successes) > 0, length(successes) == length(trials),
            all(is.finite(successes)), all(is.finite(trials)), all(trials > 0),
            all(successes >= 0 & successes <= trials),
            all(successes == floor(successes)), all(trials == floor(trials)),
            length(level) == 1L, is.finite(level), level > 0, level < 1)
  rates <- successes / trials
  draws <- length(rates)
  # Designs are sampled equally, including when nsim does not divide draws.
  power <- mean(rates)
  se <- if (draws > 1) stats::sd(rates) / sqrt(draws) else NA_real_
  if (draws == 1) {
    interval <- c(NA_real_, NA_real_)
    method <- "unavailable-one-design"
  } else if (se == 0) {
    # Identical observed proportions do not prove zero design uncertainty.
    # Use a bounded-variable Hoeffding interval rather than a zero-width CI.
    half <- sqrt(log(2 / (1 - level)) / (2 * draws))
    interval <- c(max(0, power - half), min(1, power + half))
    method <- "design-hoeffding"
  } else {
    half <- stats::qt((1 + level) / 2, df = draws - 1) * se
    interval <- c(max(0, power - half), min(1, power + half))
    method <- "design-t-approximate"
  }
  list(power = power, mcse = se, ci_lower = interval[1], ci_upper = interval[2],
       ci_method = method, ci_level = level)
}

chain_outcomes <- function(chains) {
  required <- c("any_validated_success", "any_positive_impact")
  missing <- setdiff(required, names(chains))
  if (length(missing)) stop("Chain dataset missing outcome columns: ", paste(missing, collapse = ", "))
  data.frame(success = as_binary(chains$any_validated_success),
             vep = as_binary(chains$any_positive_impact))
}
