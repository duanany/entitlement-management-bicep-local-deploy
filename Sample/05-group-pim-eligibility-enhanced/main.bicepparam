using './main.bicep'

// Export tokens or edit below before deploying
param entitlementToken = readEnvironmentVariable('ENTITLEMENT_TOKEN', '')
param groupUserToken = readEnvironmentVariable('GROUP_USER_TOKEN', '')

// Optionally override
param namePrefix = 'bicep-pim-enhanced'
param testUserId = '7a72c098-a42d-489f-a3fa-c2445dec6f9c'
param conditionalAccessContextId = 'c1'
param requireConditionalAccessContext = true

// Scenario 2 (existing group IDs)
param enableExistingGroupScenario = false
param existingEligibleGroupId = ''
param existingActivatedGroupId = ''
param existingApproverGroupId = ''
param existingConditionalAccessContextId = 'c1'
param existingRequireConditionalAccessContext = true

// Scenario 3 (auth-context only)
param enableAuthContextOnlyScenario = true
param authContextOnlyId = 'c1'
