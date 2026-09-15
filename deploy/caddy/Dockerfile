# Caddy mit DNS-Plugin. Das offizielle Image kann DNS-01 nicht - die
# ACME-Anbieter sind einzelne Module, die einkompiliert werden muessen.
#
# Cloudflare ist hier nur ein Beispiel. Fuer deSEC, Hetzner, INWX, Netcup und
# andere gibt es je ein eigenes Modul; eintragen und neu bauen.
FROM caddy:2-builder AS builder
RUN xcaddy build --with github.com/caddy-dns/cloudflare

FROM caddy:2
COPY --from=builder /usr/bin/caddy /usr/bin/caddy
