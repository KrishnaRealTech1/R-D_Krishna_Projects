# RealTech iAWS v0.1 synchronization

Transactions are saved locally first. Server synchronization runs asynchronously so a temporary network outage does not block the local weighing cycle.

For each pending iAWS transaction the worker:

1. uploads the available Camera 1-4 images using the configured upload mode;
2. stores resulting remote image paths locally;
3. publishes the iAWS transaction JSON using MQTT;
4. marks the transaction synced only after the publish succeeds;
5. retries pending/failed transactions using the configured retry settings.

Camera capture and transaction persistence therefore remain independent from immediate internet availability.
