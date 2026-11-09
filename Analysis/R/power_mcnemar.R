# Exact power for the RQ1 paired lane comparison (exact McNemar test).
#
# Power depends on the discordant probabilities p10 = P(agentic VEP, LLM not) and
# p01 = P(LLM VEP, agentic not), through q = p10 + p01 (total discordance) and
# delta = p10 - p01 (paired VEP-rate difference). Power is computed exactly: the
# discordant count D ~ Binomial(n, q), and given D the agentic-favouring count
# K ~ Binomial(D, p10 / q); the test rejects when the exact two-sided binomial test of
# K against D / 2 has p <= alpha. Exact power is saw-toothed in n, so "n for power" is
# the smallest n at which power reaches the target and stays there for the next 25 n.
#
# Run from Analysis/:
#   Rscript R/power_mcnemar.R
#   Rscript R/power_mcnemar.R --q=0.2,0.34 --delta=0.07 --n=408,367 --alpha=0.05

get_script_dir <- function() {
  file <- sub("^--file=", "", grep("^--file=", commandArgs(FALSE), value = TRUE))
  if (length(file)) dirname(normalizePath(file)) else file.path(getwd(), "R")
}
SCRIPT_DIR <- get_script_dir()
source(file.path(SCRIPT_DIR, "common.R"))

opts <- parse_args(list(
  q = c(0.12, 0.20, 0.30, 0.34, 0.40),   # 0.34: pilot estimate (77 pairs, 4 candidates)
  delta = c(0.05, 0.07, 0.10),
  n = c(300, 367, 408),                  # 367 = 90% of 408 paired-complete
  alpha = 0.10,
  targets = c(0.80, 0.90),
  max_n = 2000,
  out = file.path(SCRIPT_DIR, "output", "power_mcnemar.csv")
))

mcnemar_power <- function(n, q, delta, alpha) {
  p10 <- (q + delta) / 2
  if (delta > q || p10 > 1) return(NA_real_)
  share <- p10 / q
  d <- 0:n
  reject_prob <- vapply(d, function(dd) {
    if (dd == 0) return(0)
    k <- 0:dd
    p <- pmin(1, 2 * pbinom(pmin(k, dd - k), dd, 0.5))
    sum(dbinom(k, dd, share)[p <= alpha])
  }, numeric(1))
  sum(dbinom(d, n, q) * reject_prob)
}

required_n <- function(q, delta, alpha, target, max_n, hold = 25) {
  # Coarse scan from the normal approximation, then an exact search around it.
  za <- qnorm(1 - alpha / 2); zb <- qnorm(target)
  approx <- ceiling((za * sqrt(q) + zb * sqrt(q - delta^2))^2 / delta^2)
  lo <- max(10, floor(approx * 0.8))
  power <- vapply(lo:min(max_n, ceiling(approx * 1.3) + hold), mcnemar_power, numeric(1),
                  q = q, delta = delta, alpha = alpha)
  ok <- power >= target
  for (i in seq_along(ok)) {
    window <- ok[i:min(length(ok), i + hold)]
    if (all(window)) return(lo + i - 1)
  }
  NA_integer_
}

rows <- list()
for (q in opts$q) for (delta in opts$delta) {
  if (delta > q) next
  row <- data.frame(q = q, delta = delta, p10 = (q + delta) / 2, p01 = (q - delta) / 2,
                    alpha = opts$alpha)
  for (n in opts$n) row[[paste0("power_n", n)]] <- mcnemar_power(n, q, delta, opts$alpha)
  for (t in opts$targets)
    row[[sprintf("n_for_%02d", round(100 * t))]] <- required_n(q, delta, opts$alpha, t, opts$max_n)
  rows[[length(rows) + 1]] <- row
}
table <- do.call(rbind, rows)

dir.create(dirname(opts$out), recursive = TRUE, showWarnings = FALSE)
write.csv(table, opts$out, row.names = FALSE)
shown <- table
shown[grep("^power_", names(shown))] <- round(shown[grep("^power_", names(shown))], 3)
print(shown, row.names = FALSE)
cat("wrote", opts$out, "\n")
