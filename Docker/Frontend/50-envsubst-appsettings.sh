#!/bin/sh
set -e

envsubst < /etc/regard-frontend/appsettings.json > /usr/share/nginx/html/wwwroot/appsettings.json
rm -f /usr/share/nginx/html/wwwroot/appsettings.json.br
rm -f /usr/share/nginx/html/wwwroot/appsettings.json.gz
