
# Define the target network and container names
$network = "mall_network"
$containers = @("postgres-assort-db", "SampleOnlineMall.AssortmentApi", "SampleOnlineMall.AssortmentApi_1")

# Loop through each container and connect it to the network
foreach ($container in $containers) {
    Write-Host "Connecting container $container to network $network"
    docker network connect $network $container
}

Write-Host "All specified containers have been connected to $network."
docker network inspect mall_network
Read-Host
