# ReSS

ReSS is a very simple Discord webhook application allowing you to send RSS feed messages.

# Requirements
This application uses [Docker Compose](https://docs.docker.com/compose/install/).

# How To Use
```shell
git clone https://github.com/moeux/Ress.git
cd Ress
nano docker-compose.yml
```
## Set enivornment variables
Inside the docker compose file you need to set the RSS feed and the Discord webhook URI:
```docker
services:
  ress:
    image: ress
    build:
      context: .
      dockerfile: Ress/Dockerfile
    environment:
      RESS_FEED_URI: "YOUR_RSS_FEED_URI_HERE"
      RESS_WEBHOOK_URI: "YOUR_DISCORD_WEBHOOK_URI_HERE"
```
In order to create a webhook on Discord please see [here](https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks).

