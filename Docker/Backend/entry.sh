#!/usr/bin/env bash
envsubst < /etc/regard/appsettings.json > ./appsettings.json

# wait for db
if [ $DB_TYPE == "SqlServer" ]; then
	SERVER=$( echo "$DB_MSSQL" | sed 's/;/\n/g' | grep Server= | cut -c 8- | sed 's/,/:/' )
	./wait-for-it.sh $SERVER
fi

dotnet Regard.Backend.dll
