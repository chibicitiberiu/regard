set -e

DB_PASS='yourStrong(!)Password'

if [ ! -d "Tmp/SqlData" ]
then
    mkdir -p Tmp/SqlData
    chmod 777 Tmp/SqlData
    mkdir -p Tmp/RegardData
    chmod 777 Tmp/RegardData

    # start database
    docker-compose up -d db

    # wait a bit for database to start running
    sleep 20

    # create database, user
    docker-compose exec -T db /opt/mssql-tools/bin/sqlcmd \
        -S localhost -U sa -P "$DB_PASS" -d master << EOF

        CREATE DATABASE Regard
        GO

        USE REGARD
        GO
        
        CREATE LOGIN regard WITH PASSWORD = 'RegardSuperSecre3t!Passw0rd'
        GO

        CREATE USER regard FOR LOGIN regard
        GO

        EXEC sp_addrolemember 'db_owner', 'regard'        
        GO
EOF

    sleep 15
else
    # start database
    docker-compose up -d db
fi

# start rest of containers
docker-compose up -d
docker-compose logs -f