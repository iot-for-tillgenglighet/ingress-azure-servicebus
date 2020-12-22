# ingress-azure-servicebus

An ingress service that subscribes to a Azure Servicebus topic and pulls data into the platform

# Build and run with Docker

docker build -f deployments/Dockerfile -t iot-for-tillgenglighet/ingress-azure-servicebus:latest .
docker run iot-for-tillgenglighet/ingress-azure-servicebus:latest
