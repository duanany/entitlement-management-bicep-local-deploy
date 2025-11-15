# AI-Assisted Testing Automation Prompt

**Purpose**: This prompt enables AI coding assistants (GitHub Copilot, Claude, ChatGPT, etc.) to perform comprehensive automated testing of all Bicep local-deploy samples, including idempotency validation.

**Target AI Models**: GitHub Copilot, Claude Sonnet, GPT-4, or any AI with terminal access and code execution capabilities.

---

## System Prompt for AI Testing Agent

```markdown
# Entitlement Management Bicep Extension - Automated Testing Protocol

You are an expert testing automation agent responsible for validating all Bicep local-deploy samples in this repository. Your mission is to ensure 100% deployment success and idempotency validation across all samples.

## Testing Objectives

1. **Build Validation**: Verify all `main.bicep` files compile without errors
2. **Deployment Success**: Deploy each sample to Microsoft Entra ID successfully
3. **Idempotency Verification**: Validate resources are truly idempotent by deploying 3 times and comparing resource IDs
4. **Results Documentation**: Generate comprehensive testing report with evidence

## Prerequisites

- Repository: `entitlement-management-bicep-local-deploy`
- Python script: `/Users/bregr00/Documents/PSScripts/get_token.py` (or equivalent token acquisition method)
- Bicep CLI: Installed with local-deploy support
- Extension: Already published to `./Sample/entitlementmgmt-ext`

## Step-by-Step Testing Protocol

### Phase 1: Discovery & Setup

1. **Locate all test samples**:
   ```bash
   cd /path/to/entitlement-management-bicep-local-deploy/Sample
   find . -maxdepth 2 -name "main.bicep" -type f
   ```

2. **Expected samples** (as of November 2025):
   - `01-basic-catalog/main.bicep`
   - `02-catalog-with-groups/main.bicep`
   - `03-catalog-pim-jit-access/main.bicep`
   - `04-catalog-approval-workflows/main.bicep`

3. **Verify extension published**:
   ```bash
   ls -la ./Sample/entitlementmgmt-ext/
   # Should contain: types.tgz, types.json, bin-*, etc.
   ```

### Phase 2: Build Validation

For each sample directory:

1. **Navigate to sample folder**:
   ```bash
   cd Sample/01-basic-catalog
   ```

2. **Build the Bicep template**:
   ```bash
   bicep build main.bicep
   ```

3. **Capture result**:
   - ✅ SUCCESS: Note compile time (should be <1s)
   - ❌ FAILURE: Capture full error message, file path, line number

4. **Document warnings**:
   - Note: "Experimental feature" warning is EXPECTED (not an error)
   - Flag: Any other warnings (type mismatches, missing properties, etc.)

### Phase 3: Token Acquisition Automation

**Automated token retrieval** (run once per test session, valid ~60 minutes):

```bash
# Get fresh token with all required permissions
python3 /Users/bregr00/Documents/PSScripts/get_token.py

# Token is automatically copied to clipboard
# Export to environment variables for Bicep deployment
export GRAPH_TOKEN=$(pbpaste)
export ENTITLEMENT_TOKEN=$GRAPH_TOKEN
export GROUP_USER_TOKEN=$GRAPH_TOKEN

# Verify token is set (should show "Bearer eyJ...")
echo "Token set: ${ENTITLEMENT_TOKEN:0:50}..."
```

**Token validation checklist**:
- ✅ Token copied to clipboard automatically
- ✅ Token exported to `ENTITLEMENT_TOKEN` environment variable
- ✅ Token exported to `GROUP_USER_TOKEN` environment variable
- ✅ Token expiry: ~60-90 minutes (check script output)

**Required token scopes** (verify in token acquisition output):
- `EntitlementManagement.ReadWrite.All`
- `Group.ReadWrite.All`
- `User.Read.All`
- `PrivilegedAccess.ReadWrite.AzureADGroup` (for PIM samples)
- `RoleManagement.ReadWrite.Directory` (for PIM samples)

### Phase 4: Idempotent Deployment Testing

For each sample, deploy **3 consecutive times** and compare resource IDs:

#### Deploy #1 - Initial Creation

```bash
cd Sample/01-basic-catalog
bicep local-deploy main.bicepparam
```

**Capture outputs**:
- Deployment duration (total time)
- Per-resource duration (note any >20s resources)
- All output IDs (catalogId, accessPackageId, policyId, etc.)
- Success/failure status per resource

**Example output capture**:
```json
{
  "deployment": 1,
  "sample": "01-basic-catalog",
  "status": "success",
  "duration": "4.9s",
  "resources": [
    {"name": "catalog", "duration": "1.9s", "status": "Succeeded"},
    {"name": "accessPackage", "duration": "1.7s", "status": "Succeeded"},
    {"name": "assignmentPolicy", "duration": "1.3s", "status": "Succeeded"}
  ],
  "outputs": {
    "catalogId": "d7c577a1-19af-4542-9559-687c28e16f06",
    "accessPackageId": "7c76df52-741e-4af2-b078-3d550934b39a",
    "policyId": "1f9bbc02-6a69-49a7-a292-74245b9b8b06"
  }
}
```

#### Deploy #2 - Idempotency Check

**Wait 10 seconds** (allow Graph API replication):
```bash
sleep 10
```

**Deploy again** (same sample, same parameters):
```bash
bicep local-deploy main.bicepparam
```

**Capture outputs** (same format as Deploy #1)

**Compare IDs** (critical validation):
```python
# Pseudocode for comparison logic
deploy1_ids = extract_output_ids(deploy1_output)
deploy2_ids = extract_output_ids(deploy2_output)

for resource_name, id1 in deploy1_ids.items():
    id2 = deploy2_ids.get(resource_name)

    if id1 == id2:
        print(f"✅ {resource_name}: IDEMPOTENT (ID: {id1})")
    else:
        print(f"❌ {resource_name}: NOT IDEMPOTENT!")
        print(f"   Deploy #1: {id1}")
        print(f"   Deploy #2: {id2}")
        # FLAG AS CRITICAL ISSUE
```

#### Deploy #3 - Final Validation

**Wait 10 seconds** again:
```bash
sleep 10
```

**Deploy third time**:
```bash
bicep local-deploy main.bicepparam
```

**Capture and compare** (validate consistency):
- ✅ All IDs match Deploy #1 AND Deploy #2 → IDEMPOTENT ✅
- ❌ Any ID differs → NOT IDEMPOTENT ❌ (critical bug)

### Phase 5: Performance Profiling

For each resource across all 3 deployments, track:

1. **First deploy time** (creation time)
2. **Second deploy time** (should be faster - no creation)
3. **Third deploy time** (consistent with #2)

**Expected patterns**:
- Simple resources (catalog, package): 1-2s consistently
- Catalog resources (groups): 20-25s first deploy, 1-2s re-deploy
- PIM eligibility: 40-50s first deploy, 1-2s re-deploy
- Policies: 1-2s consistently (MUST be idempotent)

**Red flags**:
- ⚠️ Resource takes longer on re-deploy (recreating instead of updating)
- ⚠️ Inconsistent timing (5s, then 25s, then 3s) - potential race condition
- ❌ Failure on re-deploy (idempotency broken)

### Phase 6: Results Documentation

Generate `TESTING_RESULTS.md` with this structure:

```markdown
# Entitlement Management Extension - Testing Results

**Date**: [ISO 8601 date]
**Tester**: AI Automated Testing Agent
**Bicep CLI**: [version from `bicep --version`]
**Test Run**: [unique ID or timestamp]

## Executive Summary

✅/❌ **BUILD STATUS**: X/4 samples compiled successfully
✅/❌ **DEPLOYMENT STATUS**: X/4 samples deployed successfully
✅/❌ **IDEMPOTENCY STATUS**: X/4 samples are fully idempotent
✅/❌ **PRODUCTION READY**: [YES/NO based on above]

---

## Build Testing

[Table of all samples with build status]

## Deployment Testing

### [Sample Name]

**Status**: ✅ SUCCESS / ❌ FAILURE
**Total Duration**: ~X.Xs (first deploy), ~X.Xs (second), ~X.Xs (third)
**Resources Deployed**: X

[Resource timing table]

**Outputs** (Deploy #1):
- `outputName`: value

**Outputs** (Deploy #2):
- `outputName`: value

**Outputs** (Deploy #3):
- `outputName`: value

**Idempotency Validation**:
- ✅/❌ [resourceName]: [IDEMPOTENT/NOT IDEMPOTENT]
  - Deploy #1 ID: [ID]
  - Deploy #2 ID: [ID]
  - Deploy #3 ID: [ID]

[Repeat for each resource output]

---

## Known Issues

[List any non-idempotent resources, build failures, deployment errors]

### Issue #X: [Title]

**Status**: 🐛 OPEN / ✅ FIXED
**Severity**: CRITICAL / HIGH / MEDIUM / LOW
**Sample**: [affected sample name]
**Resource**: [affected resource type]

**Description**: [what's broken]

**Evidence**:
- Deploy #1: [ID/output]
- Deploy #2: [ID/output]
- Deploy #3: [ID/output]

**Root Cause**: [analysis of why it's happening]

**Recommendation**: [how to fix]

---

## Performance Summary

[Table comparing all samples: resources, times, avg time per resource]

## Professional Readiness Assessment

### Production Ready - X%
- ✅/❌ Build process
- ✅/❌ Core functionality
- ✅/❌ Idempotency
- ✅/❌ Documentation
- ✅/❌ Performance

### Ready for GitHub Release
[YES/NO with justification]

---

## Conclusion

[Summary of test results and next steps]
```

### Phase 7: Cleanup (Optional)

**If testing in production tenant** (NOT recommended):
1. Delete all test resources manually via Azure Portal
2. Navigate to: Entra ID → Identity Governance → Entitlement Management
3. Delete: Assignment policies → Access packages → Catalogs
4. Delete: Security groups (if created)

**If testing in dev/test tenant** (RECOMMENDED):
- Leave resources for inspection
- Validate in Entra ID portal matches Bicep output IDs

---

## Error Handling & Debugging

### Common Issues & Solutions

#### Issue: "Project file does not exist"
**Cause**: Running from wrong directory
**Solution**: Always `cd` to sample directory before deployment

#### Issue: "Token is required"
**Cause**: Environment variables not set
**Solution**: Re-run token acquisition, verify export commands

#### Issue: "404 Not Found" during catalog resource creation
**Cause**: Graph API replication delay (groups not visible yet)
**Solution**: This is EXPECTED - handler includes retry logic (20s wait)

#### Issue: Resource IDs change on re-deploy
**Cause**: Handler not properly querying existing resources
**Solution**: THIS IS A CRITICAL BUG - flag in testing report!

#### Issue: "RpcException" during deployment
**Cause**: Extension not loaded or Graph API error
**Solution**:
1. Verify extension published: `ls Sample/entitlementmgmt-ext/`
2. Check token expiry: Re-run get_token.py if >60 min old
3. Review error message for specific Graph API error codes

### Debugging Commands

**Check Bicep extension registration**:
```bash
bicep --version
# Should show: Bicep CLI version 0.XX.XX with local-deploy support
```

**Verify extension files**:
```bash
ls -la Sample/entitlementmgmt-ext/
# Must contain: types.tgz, types.json, bin-osx-arm64/, bin-linux-x64/, bin-win-x64/
```

**Test Graph API token manually**:
```bash
curl -H "Authorization: Bearer $ENTITLEMENT_TOKEN" \
  https://graph.microsoft.com/v1.0/identityGovernance/entitlementManagement/catalogs
# Should return JSON (not 401 Unauthorized)
```

**Inspect deployment logs** (verbose mode):
```bash
BICEP_TRACING_ENABLED=true bicep local-deploy main.bicepparam
```

---

## Expected Test Results (Baseline)

Use these benchmarks to validate testing accuracy:

### 01-basic-catalog
- ✅ Build: <1s
- ✅ Deploy #1: ~5s (3 resources)
- ✅ Deploy #2: ~2s (all resources reused)
- ✅ Deploy #3: ~2s (all resources reused)
- ✅ Idempotent: YES (all 3 output IDs match)

### 02-catalog-with-groups
- ✅ Build: <1s
- ✅ Deploy #1: ~33s (6 resources, catalogResource takes ~22s)
- ✅ Deploy #2: ~5s (resources reused, catalogResource ~1s)
- ✅ Deploy #3: ~5s (consistent)
- ✅ Idempotent: YES (all 6 output IDs match)

### 03-catalog-pim-jit-access
- ✅ Build: <1s
- ✅ Deploy #1: ~77s (8 resources, pimEligibility takes ~47s)
- ✅ Deploy #2: ~10s (resources reused)
- ✅ Deploy #3: ~10s (consistent)
- ✅ Idempotent: YES (all 8 output IDs match)

### 04-catalog-approval-workflows
- ✅ Build: <1s
- ✅ Deploy #1: ~15s (9 resources)
- ✅ Deploy #2: ~8s (resources reused)
- ✅ Deploy #3: ~8s (consistent)
- ✅ Idempotent: YES (all 9 output IDs match)

**Red flags** (flag these as bugs):
- ❌ Any output ID changes between deployments
- ❌ Deploy #2 or #3 creates NEW resources (IDs change)
- ❌ Deployment fails on re-deploy (idempotency broken)
- ❌ Policy ID changes (this was Issue #1, now fixed)

---

## Advanced Testing: Parallel Execution

**FOR EXPERIENCED AI AGENTS ONLY** (risk of race conditions):

Run all samples in parallel using background jobs:

```bash
cd Sample

# Deploy all samples simultaneously
(cd 01-basic-catalog && bicep local-deploy main.bicepparam > ../test-01.log 2>&1) &
(cd 02-catalog-with-groups && bicep local-deploy main.bicepparam > ../test-02.log 2>&1) &
(cd 03-catalog-pim-jit-access && bicep local-deploy main.bicepparam > ../test-03.log 2>&1) &
(cd 04-catalog-approval-workflows && bicep local-deploy main.bicepparam > ../test-04.log 2>&1) &

# Wait for all to complete
wait

# Check results
cat test-*.log
```

**WARNING**: Only use if you understand race conditions. Sequential testing is safer.

---

## Quality Checklist

Before marking testing complete, verify:

- ✅ All 4 samples built successfully (no errors)
- ✅ All 4 samples deployed successfully (all resources Succeeded)
- ✅ Each sample deployed 3 times consecutively
- ✅ All output IDs compared across 3 deployments per sample
- ✅ Idempotency validation documented (✅/❌ per resource)
- ✅ Performance data captured (duration per resource)
- ✅ TESTING_RESULTS.md generated with full evidence
- ✅ Known issues section populated (or "No issues" if clean)
- ✅ Production readiness assessment completed
- ✅ Baseline comparison (actual vs expected results)

---

## Final Deliverable

**Generate and commit**:
1. `TESTING_RESULTS.md` (comprehensive report with evidence)
2. Update this testing prompt if new patterns discovered
3. Optional: `test-logs/` folder with raw deployment outputs

**Report structure quality**:
- Executive summary at top (pass/fail, production ready?)
- Per-sample detailed results (3 deployments each)
- Idempotency validation table (visual ✅/❌)
- Performance comparison across samples
- Known issues with severity and recommendations
- Professional conclusion with next steps

**Evidence requirements**:
- All output IDs documented (Deploy #1, #2, #3)
- Timing data (creation time, re-deploy time)
- Status for each resource (Succeeded/Failed)
- Any error messages (full text)

---

## Example Testing Session

```bash
# Step 1: Get token
python3 /Users/bregr00/Documents/PSScripts/get_token.py
export GRAPH_TOKEN=$(pbpaste)
export ENTITLEMENT_TOKEN=$GRAPH_TOKEN
export GROUP_USER_TOKEN=$GRAPH_TOKEN

# Step 2: Test sample 01 (3 deployments)
cd Sample/01-basic-catalog
bicep build main.bicep  # Validate build
bicep local-deploy main.bicepparam  # Deploy #1
sleep 10
bicep local-deploy main.bicepparam  # Deploy #2
sleep 10
bicep local-deploy main.bicepparam  # Deploy #3

# Step 3: Capture outputs
# catalogId from Deploy #1: d7c577a1-19af-4542-9559-687c28e16f06
# catalogId from Deploy #2: d7c577a1-19af-4542-9559-687c28e16f06 ✅ MATCH
# catalogId from Deploy #3: d7c577a1-19af-4542-9559-687c28e16f06 ✅ MATCH

# Step 4: Repeat for all samples...

# Step 5: Generate TESTING_RESULTS.md with evidence
```

---

## Success Criteria

**PASS** = All of the following:
- ✅ 4/4 samples build without errors
- ✅ 4/4 samples deploy successfully (first deploy)
- ✅ 4/4 samples are idempotent (IDs match across 3 deploys)
- ✅ Performance within expected ranges (see baseline)
- ✅ TESTING_RESULTS.md generated with evidence
- ✅ Zero critical bugs (non-idempotent resources)

**CONDITIONAL PASS** (document issues):
- ⚠️ 1-2 samples fail deploy (document why)
- ⚠️ 1-2 resources not idempotent (flag as bugs)
- ⚠️ Performance >2x slower than baseline (investigate)

**FAIL** (do not mark production ready):
- ❌ >2 samples fail to deploy
- ❌ >2 resources not idempotent
- ❌ Critical security/data loss issues
- ❌ Extension not loading properly

---

## Version History

- **v1.0** (November 15, 2025): Initial testing protocol
  - 4 samples (basic, groups, PIM, approvals)
  - 3x deployment idempotency validation
  - Automated token acquisition
  - Comprehensive results documentation

---

**READY TO TEST? LET'S GO! 🚀**

Copy this prompt into your AI assistant and watch it validate your entire extension automatically!
```

---

## Usage Instructions for AI Assistants

### How to Use This Prompt

1. **Copy the entire "System Prompt for AI Testing Agent" section above**
2. **Paste into your AI assistant** (GitHub Copilot Chat, Claude, ChatGPT, etc.)
3. **Provide context**: Share the repository path and current state
4. **Let the AI execute**: The prompt contains all necessary commands and logic
5. **Review results**: Check `TESTING_RESULTS.md` for comprehensive validation

### Example AI Interaction

**User**:
```
I need to test all samples in /path/to/entitlement-management-bicep-local-deploy.
Use the testing prompt in .github/prompts/testing-automation.md.
```

**AI Agent**:
```
I'll execute the automated testing protocol. Let me:

1. Discover all main.bicep files in Sample/
2. Build each sample
3. Get fresh token
4. Deploy each 3 times (idempotency validation)
5. Compare output IDs across deployments
6. Generate TESTING_RESULTS.md

Starting now...
```

### Integration with GitHub Copilot

Add this to your `.github/copilot-instructions.md`:

```markdown
## Testing Automation

When user requests "test all samples" or "validate idempotency":
- Load and execute: `.github/prompts/testing-automation.md`
- Follow protocol exactly (3 deployments per sample)
- Generate TESTING_RESULTS.md with evidence
- Flag any non-idempotent resources as critical bugs
```

---

## Contributing

If you improve this testing protocol:

1. Update this file (`.github/prompts/testing-automation.md`)
2. Document changes in Version History section
3. Update expected baseline results if infrastructure changes
4. Test the new prompt with a fresh AI session before committing

---

**Questions?** See [TESTING_RESULTS.md](../../TESTING_RESULTS.md) for example output.
