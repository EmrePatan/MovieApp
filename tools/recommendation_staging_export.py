#!/usr/bin/env python3
"""Staging-only catalog export using the existing migration connection secret.

Never prints credentials. Requires psql and a read-only database role ideally.
Supports PostgreSQL URI and common Npgsql key/value connection strings.
"""
import json
import os
from pathlib import Path
import subprocess
import sys
from urllib.parse import unquote, urlsplit


def parse_connection(raw):
    raw = raw.strip()
    if raw.lower().startswith(("postgres://", "postgresql://")):
        parsed = urlsplit(raw)
        return {
            "PGHOST": parsed.hostname or "",
            "PGPORT": str(parsed.port or 5432),
            "PGDATABASE": unquote(parsed.path.lstrip("/")),
            "PGUSER": unquote(parsed.username or ""),
            "PGPASSWORD": unquote(parsed.password or ""),
            "PGSSLMODE": "require",
        }
    # Semicolon-separated Npgsql key/value; quoted values can contain ';'.
    parts, buf, quote = [], [], None
    for char in raw:
        if char in ("'", '"'):
            if quote == char:
                quote = None
            elif quote is None:
                quote = char
        if char == ";" and quote is None:
            parts.append("".join(buf))
            buf = []
        else:
            buf.append(char)
    if quote:
        raise ValueError("Unterminated quote in connection string")
    parts.append("".join(buf))
    fields = {}
    for part in parts:
        if not part.strip():
            continue
        if "=" not in part:
            raise ValueError("Invalid Npgsql connection segment")
        key, value = part.split("=", 1)
        value = value.strip()
        if len(value) >= 2 and value[0] == value[-1] and value[0] in ("'", '"'):
            value = value[1:-1]
        fields[key.strip().lower().replace(" ", "")] = value
    def get(*keys, default=""):
        return next((fields[k] for k in keys if k in fields), default)
    return {
        "PGHOST": get("host", "server"),
        "PGPORT": get("port", default="5432"),
        "PGDATABASE": get("database", "initialcatalog"),
        "PGUSER": get("username", "userid", "user"),
        "PGPASSWORD": get("password", "pwd"),
        "PGSSLMODE": get("sslmode", default="require").lower(),
    }


def main():
    raw = os.environ.get("MOVIEAPP_STAGING_POSTGRES_CONNECTION", "")
    if not raw:
        raise ValueError("Missing staging connection secret")
    env = os.environ.copy()
    env.update(parse_connection(raw))
    env.pop("MOVIEAPP_STAGING_POSTGRES_CONNECTION", None)
    if not all(env[k] for k in ("PGHOST", "PGDATABASE", "PGUSER")):
        raise ValueError("Connection missing host, database or username")
    # Fail closed before attempting to query any non-staging database.
    if "staging" not in env["PGDATABASE"].lower():
        raise ValueError("Refusing non-staging database (name must contain staging)")
    env["PGOPTIONS"] = "-c default_transaction_read_only=on -c statement_timeout=60000"
    env["PGCONNECT_TIMEOUT"] = "10"
    dest = Path(os.environ["RUNNER_TEMP"]) / "recommendation-catalog.json"
    query = Path("tools/sql/recommendation_candidate_export_staging.sql")
    with dest.open("w", encoding="utf-8") as output:
        proc = subprocess.run(
            ["psql", "-X", "-q", "-v", "ON_ERROR_STOP=1", "-At", "-f", str(query)],
            env=env, stdout=output, stderr=subprocess.PIPE, text=True,
            timeout=180, check=False,
        )
    if proc.returncode:
        dest.unlink(missing_ok=True)
        # Do not emit psql stderr, which may include connection information.
        raise RuntimeError(f"Read-only staging export failed (psql exit {proc.returncode})")
    data = json.loads(dest.read_text(encoding="utf-8"))
    if not isinstance(data.get("candidates"), list):
        raise ValueError("Unexpected export JSON")
    print(f"Staging catalog exported: {len(data['candidates'])} candidates (capped).")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"::error::{type(exc).__name__}: {str(exc)}", file=sys.stderr)
        sys.exit(1)
