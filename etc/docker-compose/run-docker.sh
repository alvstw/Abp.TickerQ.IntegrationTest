#!/bin/bash

if [[ ! -d certs ]]
then
    mkdir certs
    cd certs/
    if [[ ! -f localhost.pfx ]]
    then
        dotnet dev-certs https -v -ep localhost.pfx -p 87432c65-7c62-4095-990c-98fd6d152476 -t
    fi
    cd ../
fi

docker-compose up -d
