using './main.bicep'

// Get token: python3 Scripts/get_access_token.py
param entitlementToken = readEnvironmentVariable('ENTITLEMENT_TOKEN', '')
