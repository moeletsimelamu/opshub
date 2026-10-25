#!/usr/bin/env bash
# Checks a running OpsHub end to end: a local production run, the container in CI, or a
# real deployment. Read-only: it signs in and reads, and changes nothing.
#
# Usage:  SMOKE_PASSWORD='<password>' scripts/smoke-test.sh <base-url> <email>
#   e.g.  SMOKE_PASSWORD="$ADMIN_PASSWORD" scripts/smoke-test.sh https://opshub.example.com admin@example.com
# The address must be HTTPS: the API refuses plain HTTP (its cookies are Secure-only).
# The password comes from an environment variable, so it doesn't appear in the process list.
set -euo pipefail

base="${1:?base URL, e.g. https://localhost:8443}"
email="${2:?email of an account to sign in with}"
password="${SMOKE_PASSWORD:?set SMOKE_PASSWORD to the password of that account}"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
jar="$work/cookies" body="$work/body" headers="$work/headers"

# Every request goes through curl. SMOKE_CA_CERT optionally names an extra certificate authority
# to trust, such as the throwaway proxy's own one in CI: TLS is still verified, never skipped.
http() { curl ${SMOKE_CA_CERT:+--cacert "$SMOKE_CA_CERT"} "$@"; }
ok()   { printf '  ok    %s\n' "$1"; }
fail() { printf '  FAIL  %s\n' "$1" >&2; exit 1; }
json_escape() { local s=$1; s=${s//\\/\\\\}; s=${s//'"'/'\"'}; printf '%s' "$s"; }
header() { grep -i "^$1:" "$headers" | head -n 1 | cut -d' ' -f2- | tr -d '\r'; }

echo "Smoke test of $base"

# 1. Wait for the app: it applies migrations before it starts listening.
for attempt in $(seq 1 60); do
  if http -fsS -o /dev/null "$base/health" 2>/dev/null; then break; fi
  [ "$attempt" -eq 60 ] && fail "/health did not answer within 60 seconds"
  sleep 1
done
ok "/health answers"

# 2. A bookmarked screen gets the Angular page, with the page security policy.
code=$(http -sS -D "$headers" -o "$body" -w '%{http_code}' "$base/tickets/1")
[ "$code" = 200 ] || fail "/tickets/1 returned $code, expected 200"
grep -q '<app-root>' "$body" || fail "/tickets/1 is not the Angular page"
header Content-Security-Policy | grep -q "script-src 'self'" || fail "the page has no script-src 'self' policy"
[ "$(header X-Content-Type-Options)" = nosniff ] || fail "X-Content-Type-Options: nosniff is missing"
ok "pages serve the Angular app with its security headers"

# 3. The API refuses anonymous calls with a JSON error, not a page or a redirect.
result=$(http -sS -o "$body" -w '%{http_code} %{content_type}' "$base/api/auth/me")
case "$result" in
  "401 application/problem+json"*) ;;
  *) fail "/api/auth/me signed out returned '$result'" ;;
esac
ok "signed-out API calls get 401 ProblemDetails"

# 4. Sign in the way the browser does: fetch the XSRF token, then send it back as a header.
http -sS -c "$jar" -b "$jar" -o /dev/null "$base/api/auth/antiforgery"
token=$(awk '$6 == "XSRF-TOKEN" { print $7 }' "$jar")
[ -n "$token" ] || fail "no XSRF-TOKEN cookie was issued"
code=$(http -sS -c "$jar" -b "$jar" -o "$body" -w '%{http_code}' \
  -H 'Content-Type: application/json' -H "X-XSRF-TOKEN: $token" \
  --data "{\"email\":\"$(json_escape "$email")\",\"password\":\"$(json_escape "$password")\"}" \
  "$base/api/auth/login")
[ "$code" = 200 ] || fail "sign-in returned $code: $(cat "$body")"
ok "signed in as $email"

# 5. The session works, and reference data exists (a ticket can be filed).
code=$(http -sS -b "$jar" -o "$body" -w '%{http_code}' "$base/api/categories")
[ "$code" = 200 ] || fail "/api/categories returned $code"
grep -q '"name"' "$body" || fail "there are no ticket categories"
ok "the session works and ticket categories exist"

echo "All checks passed."
