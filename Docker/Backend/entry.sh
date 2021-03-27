envsubst < /etc/regard/appsettings.json > ./appsettings.json

# wait for db
SERVER=$( echo "$DB_MSSQL" | sed 's/;/\n/g' | grep Server= | cut -c 8- | sed 's/,/:/' )
./wait-for-it.sh $SERVER

dotnet Regard.Backend.dll