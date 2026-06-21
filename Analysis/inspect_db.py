import sqlite3

dbs = [
    r"D:\Projects\TestMap\TestMap\Output\consulthunter-TestMap-Example\analysis.db",
    r"D:\Projects\TestMap\TestMap\Output\dotnetcore-aspectcore-framework\analysis.db",
]

for db_path in dbs:
    repo = db_path.split("\\")[-2]
    print(f"=== {repo} ===")
    con = sqlite3.connect(db_path)
    tables = [r[0] for r in con.execute(
        "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name"
    ).fetchall()]
    print("Tables:", tables)
    for t in tables:
        count = con.execute(f"SELECT COUNT(*) FROM [{t}]").fetchone()[0]
        cols = [r[1] for r in con.execute(f"PRAGMA table_info([{t}])").fetchall()]
        print(f"  {t}: {count} rows")
        print(f"    cols: {cols}")
    con.close()
    print()
