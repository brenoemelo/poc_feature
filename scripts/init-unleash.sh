
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

# 4. Ensure Admin User
echo "Ensuring Admin User..."
# Get Admin ID if exists
ADMIN_ID=$(curl -s -H "Authorization: $ADMIN_TOKEN" "$API_URL/admin/user-admin" | jq -r '.users[] | select(.username=="admin") | .id')

if [ -z "$ADMIN_ID" ] || [ "$ADMIN_ID" = "null" ]; then
  echo "Creating admin user..."
  curl -s -X POST -H "Authorization: $ADMIN_TOKEN" -H "Content-Type: application/json" \
    -d '{
      "username": "admin",
      "name": "Admin",
      "email": "admin@example.com",
      "rootRole": 1,
      "password": "password"
    }' \
    "$API_URL/admin/user-admin" > /dev/null
else
  echo "Updating admin user (ID: $ADMIN_ID)..."
  curl -s -X PUT -H "Authorization: $ADMIN_TOKEN" -H "Content-Type: application/json" \
    -d '{
      "username": "admin",
      "name": "Admin",
      "email": "admin@example.com",
      "rootRole": 1,
      "password": "password"
    }' \
    "$API_URL/admin/user-admin/$ADMIN_ID" > /dev/null
fi

# 5. Ensure Backup Admin User (admin2)
echo "Ensuring Backup Admin User (admin2)..."
ADMIN2_ID=$(curl -s -H "Authorization: $ADMIN_TOKEN" "$API_URL/admin/user-admin" | jq -r '.users[] | select(.username=="admin2") | .id')

if [ -z "$ADMIN2_ID" ] || [ "$ADMIN2_ID" = "null" ]; then
  echo "Creating backup admin user 'admin2'..."
  curl -s -X POST -H "Authorization: $ADMIN_TOKEN" -H "Content-Type: application/json" \
    -d '{
      "username": "admin2",
      "name": "Admin Backup",
      "email": "admin2@example.com",
      "rootRole": 1,
      "password": "password"
    }' \
    "$API_URL/admin/user-admin" > /dev/null
else
  echo "Backup admin user 'admin2' already exists."
fi

echo "Ensure you can login with user 'admin' or 'admin2' and password 'password'."
echo "Unleash initialization and configuration complete."
