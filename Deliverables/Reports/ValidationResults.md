# Local validation results

Validation date: 3 November 2026.

## Release configuration

Command, from the repository root:

```powershell
dotnet test Backend/BlueHarvest.PricingExceptions/BlueHarvest.PricingExceptions.slnx --configuration Release --no-restore --logger trx --results-directory Backend/BlueHarvest.PricingExceptions/TestResults
```

Result: 27 passed, 0 failed, 0 skipped.

| Suite | Passed | Scope |
| --- | --- | --- |
| PricingExceptions.UnitTests | 18 | Service workflow, terms validation, history, retries, roles, version conflicts, competing commands, cancellation and historical record protection |
| PricingExceptions.IntegrationTests | 9 | ASP.NET Core HTTP pipeline, create/approve/revise/reapprove, decline, exact retry response, input errors, identity/role failures, Swagger and disabled Development identity in Production |

The solution compiled in Release configuration without reported compiler warnings or errors in this run. TRX files are generated under TestResults and ignored by Git.

The assumptions Word document was regenerated from Markdown and checked as an Open XML package: all package XML parts parsed successfully, with 73 paragraphs and one assumptions table. This verifies package structure and content, not a visual rendering in Microsoft Word.

The CI workflow has been added but has not been executed remotely during this session. The local tests use EF Core InMemory and do not prove relational constraints, rollback after storage failures, or persistent retries across restarts.

## Fresh local checkout verification

The committed application source at b36449a was cloned into a separate temporary directory without existing build outputs. Restore succeeded with NuGet access, the Release build completed with 0 warnings and 0 errors, and all 27 tests passed (18 service, 9 HTTP integration).

The cloned API then started in Development on port 5268. Its live smoke-test script passed against that port, covering create, replay, conflicting key reuse, approval, approved-discount retrieval, revision, history, validation, authentication, authorization, Swagger and OpenAPI.

This validates the committed application source independently of the workspace's build outputs. The delivery directory and refreshed documentation are new working-tree changes prepared after that source commit. No new commit or remote CI run was created by this verification.
