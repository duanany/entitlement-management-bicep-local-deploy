using './main.bicep'

// Get token: python3 /Users/bregr00/Documents/PSScripts/get_token.py
param entitlementToken = readEnvironmentVariable('ENTITLEMENT_TOKEN', '')

// Replace with your user's Object ID and group ID
param testUserId = '7a72c098-a42d-489f-a3fa-c2445dec6f9c'
param testGroupId = '0afd1da6-51fb-450f-bf1a-069a85dcacad'
