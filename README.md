# ingress-azure-servicebus

An ingress service that subscribes to an Azure Servicebus topic and pulls data into the platform

# Build with Docker

docker build -f deployments/Dockerfile -t iot-for-tillgenglighet/ingress-azure-servicebus:latest .

# Build and test with Docker Compose

Set the INGRESS_ASB_CONNSTR environment variable to the connection string that should be used. Then build and run the service with the following commands:

docker-compose -f deployments/docker-compose.yml build
docker-compose -f deployments/docker-compose.yml up
