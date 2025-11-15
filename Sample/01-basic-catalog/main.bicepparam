using './main.bicep'

// Get token: python3 /Users/bregr00/Documents/PSScripts/get_token.py
param entitlementToken = readEnvironmentVariable('ENTITLEMENT_TOKEN', '')
