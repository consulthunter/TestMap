# Shared helpers for the R analysis scripts (complexity GLMM, power simulation).

# The structural metric family collapsed into complexity_index. The value is the
# sign that makes "higher = more complex": maintainability index is inverse.
STRUCTURAL_METRICS <- c(
  maintainability_index = -1,
  cyclomatic_complexity = 1,
  class_coupling = 1,
  depth_of_inheritance = 1,
  source_lines_of_code = 1,
  executable_lines_of_code = 1
)

# Right-skewed counts, log1p-transformed before standardizing. MI is bounded 0-100.
LOG_METRICS <- setdiff(names(STRUCTURAL_METRICS), "maintainability_index")

# The candidate-level predictor GLMM. Shared by the power simulation and the analysis fit
# so both test exactly the same model.
glmm_formula <- function(predictor) {
  stats::as.formula(paste("y ~", predictor, "+ lane + (1 | repo/candidate) + (1 | config)"))
}
COMPLEXITY_GLMM_FORMULA <- glmm_formula("complexity_index")

#' Parse `--name=value` arguments against a list of typed defaults.
#'
#' Numeric defaults accept comma-separated lists (`--odds-ratios=1.5,1.3`); character
#' defaults of length > 1 do too. Dashes in names map to underscores.
parse_args <- function(defaults, args = commandArgs(trailingOnly = TRUE)) {
  opts <- defaults
  for (arg in args) {
    if (arg %in% c("-h", "--help")) {
      cat("Options (defaults):\n")
      for (name in names(defaults)) {
        cat(sprintf("  --%s=%s\n", gsub("_", "-", name),
                    paste(format(defaults[[name]]), collapse = ",")))
      }
      quit(status = 0)
    }
    match <- regmatches(arg, regexec("^--([A-Za-z0-9_-]+)=(.*)$", arg))[[1]]
    if (length(match) != 3) stop("Unrecognized argument '", arg, "' (use --name=value)")
    key <- gsub("-", "_", match[2])
    if (!key %in% names(defaults)) stop("Unknown option --", match[2])
    value <- match[3]
    default <- defaults[[key]]
    opts[[key]] <- if (is.numeric(default)) {
      parsed <- as.numeric(strsplit(value, ",", fixed = TRUE)[[1]])
      if (anyNA(parsed) && !identical(toupper(value), "NA")) stop("--", match[2], " expects numbers")
      parsed
    } else if (is.logical(default)) {
      as.logical(value)
    } else if (length(default) > 1) {
      strsplit(value, ",", fixed = TRUE)[[1]]
    } else {
      value
    }
  }
  opts
}

#' Coerce a pandas-exported boolean column ("True"/"False"/"") to 1/0/NA.
as_binary <- function(x) {
  x <- toupper(trimws(as.character(x)))
  ifelse(x %in% c("TRUE", "1"), 1L, ifelse(x %in% c("FALSE", "0"), 0L, NA_integer_))
}

#' Fit the complexity_index transform on a reference corpus and freeze it.
#'
#' Skewed counts are log1p-transformed, every metric is z-scored with the corpus mean/SD
#' and sign-aligned (MI enters negative), zero-variance metrics are dropped, and the
#' weights are the first principal component ("pc1") or equal weights ("zmean"). The PC1
#' sign is anchored to source lines of code, so higher always means more complex.
#' The returned transform is applied unchanged to other data with
#' apply_complexity_transform(); only the final per-SD scaling is done on the sample.
#'
#' @param metrics data frame with columns named as in STRUCTURAL_METRICS.
#' @return list(method, metrics, center, scale, signs, loadings, pc1_share, dropped, correlation).
fit_complexity_transform <- function(metrics, method = c("pc1", "zmean")) {
  method <- match.arg(method)
  cols <- intersect(names(STRUCTURAL_METRICS), names(metrics))
  x <- log_metrics(metrics[cols])

  varies <- vapply(x, function(v) isTRUE(stats::sd(v, na.rm = TRUE) > 0), logical(1))
  dropped <- cols[!varies]
  x <- x[varies]
  if (ncol(x) < 2) stop("complexity_index needs at least two varying metrics; got: ", names(x))

  center <- vapply(x, mean, numeric(1), na.rm = TRUE)
  scale <- vapply(x, stats::sd, numeric(1), na.rm = TRUE)
  signs <- STRUCTURAL_METRICS[names(x)]
  z <- standardize_metrics(x, center, scale, signs)
  complete <- stats::complete.cases(z)
  pca <- stats::prcomp(z[complete, , drop = FALSE], center = FALSE, scale. = FALSE)
  loadings <- pca$rotation[, 1]
  anchor <- if ("source_lines_of_code" %in% names(loadings)) "source_lines_of_code" else names(loadings)[1]
  if (loadings[[anchor]] < 0) loadings <- -loadings
  if (method == "zmean") loadings[] <- 1 / length(loadings)

  list(
    method = method,
    metrics = names(x),
    center = as.list(center),
    scale = as.list(scale),
    signs = as.list(signs),
    loadings = as.list(loadings),
    pc1_share = pca$sdev[1]^2 / sum(pca$sdev^2),
    dropped = dropped,
    correlation = stats::cor(z, use = "complete.obs")
  )
}

#' Apply a frozen transform: the raw index score (not yet scaled to the sample's SD).
apply_complexity_transform <- function(metrics, transform) {
  cols <- unlist(transform$metrics)
  missing <- setdiff(cols, names(metrics))
  if (length(missing)) stop("Metrics missing for complexity_index: ", paste(missing, collapse = ", "))
  z <- standardize_metrics(log_metrics(metrics[cols]), unlist(transform$center),
                           unlist(transform$scale), unlist(transform$signs))
  drop(z %*% unlist(transform$loadings)[cols])
}

#' The analysis unit: z-score the raw index over the sample, so an OR is per SD of it.
scale_complexity_index <- function(raw) as.numeric(scale(raw))

log_metrics <- function(metrics) {
  x <- as.data.frame(lapply(metrics, as.numeric))
  for (col in intersect(LOG_METRICS, names(x))) x[[col]] <- log1p(pmax(x[[col]], 0))
  x
}

standardize_metrics <- function(x, center, scale, signs) {
  z <- sweep(sweep(as.matrix(x), 2, center[names(x)], `-`), 2, scale[names(x)], `/`)
  sweep(z, 2, signs[names(x)], `*`)
}
