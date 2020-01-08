FROM hotel_spawner:latest

# Setup ubuntu for running unity
RUN ["apt", "update"]
RUN ["apt", "install", "ca-certificates", "-y"]

# Copy game server data
COPY . /data
