#!/usr/bin/env bash
docker run -d --hostname tapeti --name rabbitmq-tapeti -p 5672:5672 -p 15672:15672 rabbitmq:4-management
