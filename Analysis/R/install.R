# Install the R packages used by the scripts in Analysis/R into the user library.
#   Rscript R/install.R

packages <- c("lme4", "simr", "jsonlite")
lib <- Sys.getenv("R_LIBS_USER")
dir.create(lib, recursive = TRUE, showWarnings = FALSE)
.libPaths(c(lib, .libPaths()))
missing <- setdiff(packages, rownames(installed.packages()))
if (length(missing)) install.packages(missing, lib = lib, repos = "https://cloud.r-project.org")
for (p in packages) cat(sprintf("%-10s %s\n", p, as.character(packageVersion(p))))
