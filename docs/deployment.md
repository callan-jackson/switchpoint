

## Region restrictions on Azure SQL (recorded 7 September 2026)

The free-trial subscription used for this project is **restricted from provisioning `Microsoft.Sql` in UK South**:

```
ProvisioningDisabled: Subscriptions are restricted from provisioning in this region.
Please choose a different region.
```

Azure SQL provisions normally in **Sweden Central, UK West and North Europe** on the same subscription, so the
deployment uses `-p location=swedencentral` while the resource group itself stays in UK South. Two ways round it:

```bash
# 1. Deploy the resources into a region where SQL is available (what this project does)
az deployment group create -g switchpoint-rg -f infra/main.bicep -p infra/main.bicepparam -p location=swedencentral

# 2. Skip Azure SQL entirely and run on SQLite in App Service persistent storage (free, single instance)
az deployment group create -g switchpoint-rg -f infra/main.bicep -p infra/main.bicepparam -p useSqlServer=false
```

With `useSqlServer=false` the web app receives `Database__Provider=Sqlite` and
`ConnectionStrings__SwitchPoint=Data Source=/home/data/switchpoint.db`; `/home` is App Service's persistent share, so
the database and the generated reports survive restarts. No Key Vault secret is needed for the connection string in
that mode, and the schema is created at startup.

## GitHub OIDC subject claims (recorded 7 September 2026)

The deploy workflow authenticates to Azure with a federated credential rather than a stored secret. GitHub now
issues the `sub` claim with the numeric owner and repository ids appended, even though the repository's OIDC
customisation reports `use_default: true` and `use_immutable_subject: false`:

```
repo:callan-jackson@200102296/switchpoint@1359366195:environment:production
```

A credential registered against the human-readable subject
(`repo:callan-jackson/switchpoint:environment:production`) does **not** match it, and the login fails with:

```
AADSTS700213: No matching federated identity record found for presented assertion subject '…'
```

Check the exact subject the repository will present before creating the credential:

```bash
gh api repos/<owner>/<repo>/actions/oidc/customization/sub    # sub_claim_prefix is the prefix to use
```

then register that subject on the Entra application. The `:environment:<name>` form applies because the deploy job
declares `environment: production`; a job without an environment presents `:ref:refs/heads/main` instead. Keeping
both registered costs nothing and survives a change to the workflow.

```bash
az ad app federated-credential create --id <app object id> --parameters '{
  "name": "github-env-production",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<owner>@<owner id>/<repo>@<repo id>:environment:production",
  "audiences": ["api://AzureADTokenExchange"]
}'
```

The ids are stable across renames, so this form is in fact more durable than the name-based one.
