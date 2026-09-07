

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
