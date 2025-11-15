using './main.bicep'

// Get tokens: python3 Scripts/get_access_token.py
param entitlementToken = readEnvironmentVariable('ENTITLEMENT_TOKEN', '')
param groupUserToken = readEnvironmentVariable('GROUP_USER_TOKEN', '')

// Replace with your user's Object ID
param testUserId = '7a72c098-a42d-489f-a3fa-c2445dec6f9c'
