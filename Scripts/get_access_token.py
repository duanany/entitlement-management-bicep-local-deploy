#!/usr/bin/env python3
"""
Get Microsoft Graph token with EntitlementManagement.ReadWrite.All permission
Uses interactive browser authentication to satisfy Conditional Access policies
"""

import msal
import sys
import json
import pyperclip

# Entra ID application configuration
# Using the well-known Microsoft Graph PowerShell client ID (works for delegated auth)
CLIENT_ID = "14d82eec-204b-4c2f-b7e8-296a70dab67e"  # Microsoft Graph PowerShell app
TENANT_ID = "common"  # Use "common" for multi-tenant or your specific tenant ID
SCOPES = ["https://graph.microsoft.com/EntitlementManagement.ReadWrite.All"]

def get_token():
    """Get access token using interactive browser authentication"""

    print("🔐 Authenticating to Microsoft Graph...")
    print(f"   Scope: {SCOPES[0]}")

    # Create MSAL public client app
    app = msal.PublicClientApplication(
        CLIENT_ID,
        authority=f"https://login.microsoftonline.com/{TENANT_ID}"
    )

    # Try to get token from cache first
    accounts = app.get_accounts()
    result = None

    if accounts:
        print(f"\n✅ Found cached account: {accounts[0]['username']}")
        print("   Attempting silent authentication...")
        result = app.acquire_token_silent(SCOPES, account=accounts[0])

    if not result:
        print("\n🌐 Opening browser for interactive authentication...")
        print("   Please sign in and grant consent when prompted.")

        # Interactive authentication (opens browser)
        result = app.acquire_token_interactive(
            scopes=SCOPES,
            prompt="select_account"  # Force account selection
        )

    if "access_token" in result:
        token = result["access_token"]

        print("\n✅ Authentication successful!")
        print(f"   Account: {result.get('account', {}).get('username', 'Unknown')}")
        print(f"   Token expires in: {result.get('expires_in', 'Unknown')} seconds")

        # Copy to clipboard
        try:
            pyperclip.copy(token)
            print("\n✅ Token copied to clipboard!")
        except Exception as e:
            print(f"\n⚠️  Could not copy to clipboard: {e}")
            print(f"\n📋 Your token:\n{token}\n")

        # Print export commands
        print("\n💡 To set environment variable:")
        print("   PowerShell:")
        print(f'   $env:GRAPH_TOKEN = "{token}"')
        print("\n   Bash/Zsh:")
        print(f'   export GRAPH_TOKEN="{token}"')

        print("\n🚀 Deploy your catalog!!")

        return token
    else:
        print("\n❌ Authentication failed!")
        print(f"   Error: {result.get('error', 'Unknown error')}")
        print(f"   Description: {result.get('error_description', 'No description')}")
        sys.exit(1)

if __name__ == "__main__":
    try:
        get_token()
    except KeyboardInterrupt:
        print("\n\n❌ Cancelled by user")
        sys.exit(1)
    except Exception as e:
        print(f"\n❌ Error: {e}")
        sys.exit(1)
