# -*- coding: utf-8 -*-
"""Apply EF migrations baseline SQL. Expects CALE_DATABASE_URL in the environment."""
from __future__ import annotations

import os
import sys
from pathlib import Path

import psycopg

url = os.environ.get("CALE_DATABASE_URL", "").strip()
if not url:
    print("CALE_DATABASE_URL is required", file=sys.stderr)
    sys.exit(1)

if "sslmode" not in url:
    url += ("&" if "?" in url else "?") + "sslmode=require"

sql_path = Path(__file__).resolve().parent / "baseline-ef-migrations.sql"
sql = sql_path.read_text(encoding="utf-8")

with psycopg.connect(url) as conn:
    conn.execute(sql)
    conn.commit()
    rows = conn.execute(
        'SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY 1'
    ).fetchall()

print(f"OK — __EFMigrationsHistory has {len(rows)} row(s):")
for migration_id, product_version in rows:
    print(f"  {migration_id}  ({product_version})")
