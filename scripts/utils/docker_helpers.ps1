# Docker Helpers
function Assert-DockerRunning {
    docker info > $null
    if ($LASTEXITCODE -ne 0) { throw "Docker is not running." }
}
