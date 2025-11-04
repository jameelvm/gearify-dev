#!/bin/bash

echo "Initializing AWS SES for LocalStack..."

# Verify email addresses for testing
awslocal ses verify-email-identity --email-address noreply@gearify.com
awslocal ses verify-email-identity --email-address test@example.com

# Configure SES to allow sending from verified domains
awslocal ses verify-domain-identity --domain gearify.com

echo "SES email identities verified successfully"
