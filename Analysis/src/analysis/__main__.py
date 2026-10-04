"""Entry point for ``python -m analysis``."""
from analysis.cli import main

if __name__ == "__main__":
    # Commands take glob patterns and expand them themselves; click's Windows expansion
    # would split one --db pattern into many positional arguments.
    main(windows_expand_args=False)
