import os

def search_word_in_tests(root_dir, word):
    matches = []

    for dirpath, _, filenames in os.walk(root_dir):
        for file in filenames:
            full_path = os.path.join(dirpath, file)

            try:
                with open(full_path, "r", encoding="utf-8") as f:
                    for line_num, line in enumerate(f, 1):
                        if word in line:
                            matches.append((full_path, line_num, line.strip()))
            except Exception as e:
                print(f"Could not read {full_path}: {e}")

    return matches


if __name__ == "__main__":
    output_file = "search_results2.txt"

    results = search_word_in_tests(
        "D:/Projects/TestMap/Replication/collect_tests_filtering/full_logs - Copy/full_logs",
        "FAILED"
    )

    with open(output_file, "w", encoding="utf-8") as out:
        out.write(f"Found {len(results)} matches:\n\n")
        for file, line_num, line in results:
            out.write(f"{file} (line {line_num}): {line}\n")

    print(f"Done. Results written to {output_file}")