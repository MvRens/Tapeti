#
# For use inside the Docker container
#

# We can't bind to 0.0.0.0 since we want to bind to the same port as sphinx-autobuild
CONTAINER_IP=$(ip -4 -o addr show eth0 | awk '{print $4}' | cut -d'/' -f1)

cat <<EOF > /etc/nginx/sites-available/default.conf
map \$http_upgrade \$connection_upgrade {
  default upgrade;
  ''      close;
}

server {
    listen $CONTAINER_IP:8000 default_server;

    location / {
        proxy_pass http://127.0.0.1:8000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection \$connection_upgrade;
    }
}
EOF

ln -s /etc/nginx/sites-available/default.conf /etc/nginx/sites-enabled/default.conf

echo ">> /etc/nginx/sites-available/default.conf"
cat /etc/nginx/sites-available/default.conf
echo "<<"
echo ""

nginx -t
service nginx start
sphinx-autobuild /docs ./_build/html -N