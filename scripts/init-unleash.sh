
#!/bin/sh

API_URL="http://unleash:4242/api"
ADMIN_TOKEN="*:*.admin-token"
CLIENT_TOKEN="*:development.unleash-insecure-api-token"
APP_NAME="poc-app"
INSTANCE_ID="unleash-init"

echo "Starting Unleash initialization..."

# 1. Wait for Unleash to be ready
echo "Waiting for Unleash API at $API_URL..."
until curl -s -f -H "Authorization: $ADMIN_TOKEN" "$API_URL/admin/projects/default/features" > /dev/null; do
  echo "Unleash is not ready yet. Retrying in 5 seconds..."
  sleep 5
done
echo "Unleash is up!"

# 2. Create Features
echo "Creating Features..."
FEATURES="price-calculation count-all-prices view-all-prices price-ingestion materials-crud costing-batch population-jobs view-all-components"

for FEATURE in $FEATURES; do
  # Check if feature exists
  # We check if the name matches the feature name to handle 404/error responses gracefully
  NAME=$(curl -s -H "Authorization: $ADMIN_TOKEN" "$API_URL/admin/projects/default/features/$FEATURE" | jq -r '.name')
  
  if [ "$NAME" != "$FEATURE" ]; then
    echo "Creating feature '$FEATURE'..."
    curl -s -X POST -H "Authorization: $ADMIN_TOKEN" -H "Content-Type: application/json" \
      -d "{
        \"name\": \"$FEATURE\",
        \"description\": \"Feature created by init script\",
        \"type\": \"release\",
        \"project\": \"default\",
        \"enabled\": true,
        \"strategies\": [{ \"name\": \"default\" }]
      }" \
      "$API_URL/admin/projects/default/features" > /dev/null
      
    # Enable for environment 'development'
    echo "Enabling '$FEATURE' for development..."
    curl -s -X POST -H "Authorization: $ADMIN_TOKEN" -H "Content-Type: application/json" \
      -d "{ \"enabled\": true }" \
      "$API_URL/admin/projects/default/features/$FEATURE/environments/development/on" > /dev/null
  else
    echo "Feature '$FEATURE' already exists."
    # Ensure enabled even if exists
    curl -s -X POST -H "Authorization: $ADMIN_TOKEN" -H "Content-Type: application/json" \
      -d "{ \"enabled\": true }" \
      "$API_URL/admin/projects/default/features/$FEATURE/environments/development/on" > /dev/null
  fi
done

# 3. Update appsettings.json files
echo "Updating appsettings.json files..."

# Find all appsettings.json files in /src (mounted volume)
# We exclude bin/obj directories to avoid updating build artifacts
find /src -name "appsettings.json" -not -path "*/bin/*" -not -path "*/obj/*" | while read file; do
  echo "Updating $file..."
  
  # Use jq to update the FeatureFlags section
  # We use a temporary file to store the result
  tmp=$(mktemp)
  
  # Update FeatureFlags object with Unleash configuration
  # Preserves other settings
  jq ".FeatureFlags = {
    \"Provider\": \"Unleash\",
    \"UnleashApiUrl\": \"http://unleash:4242/api/\",
    \"UnleashApiKey\": \"$CLIENT_TOKEN\",
    \"UnleashAppName\": \"$APP_NAME\",
    \"UnleashInstanceId\": \"$INSTANCE_ID\"
  }" "$file" > "$tmp" && mv "$tmp" "$file"
done

echo "Ensure you can login with user 'admin' and password 'password'."
echo "Unleash initialization and configuration complete."
