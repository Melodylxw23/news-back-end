# PowerShell script to fix migration conflicts

Write-Host "=== Database Migration Conflict Resolution Script ===" -ForegroundColor Cyan
Write-Host ""

Write-Host "This script helps resolve the 'AspNetRoles already exists' migration error." -ForegroundColor Yellow
Write-Host "Choose one of the following options:" -ForegroundColor Yellow
Write-Host ""

Write-Host "1. DROP DATABASE and recreate (DESTRUCTIVE - loses all data)" -ForegroundColor Red
Write-Host "2. Reset migration history (keeps data, but risky)" -ForegroundColor Yellow  
Write-Host "3. Show current migration status" -ForegroundColor Green
Write-Host "4. Exit" -ForegroundColor Gray
Write-Host ""

$choice = Read-Host "Enter your choice (1-4)"

switch ($choice) {
    "1" {
        Write-Host "WARNING: This will DELETE all data in your database!" -ForegroundColor Red
        $confirm = Read-Host "Type 'DELETE' to confirm"
        if ($confirm -eq "DELETE") {
   Write-Host "Dropping database..." -ForegroundColor Yellow
   dotnet ef database drop --force
        Write-Host "Database dropped. Restart your application to recreate it." -ForegroundColor Green
     } else {
 Write-Host "Operation cancelled." -ForegroundColor Gray
        }
  }
    "2" {
        Write-Host "WARNING: This approach keeps your data but may cause inconsistencies!" -ForegroundColor Yellow
        Write-Host "Only use this if you understand the risks." -ForegroundColor Yellow
        $confirm = Read-Host "Type 'RESET' to confirm"
        if ($confirm -eq "RESET") {
        Write-Host "This option requires manual SQL commands to fix migration history." -ForegroundColor Red
         Write-Host "Consider using option 1 instead for a clean solution." -ForegroundColor Red
     } else {
 Write-Host "Operation cancelled." -ForegroundColor Gray
        }
 }
    "3" {
        Write-Host "Checking migration status..." -ForegroundColor Green
 Write-Host ""
        Write-Host "=== Applied Migrations ===" -ForegroundColor Cyan
        dotnet ef migrations list
        Write-Host ""
        Write-Host "=== Database Info ===" -ForegroundColor Cyan
        dotnet ef dbcontext info
    }
    "4" {
        Write-Host "Exiting..." -ForegroundColor Gray
    }
    default {
     Write-Host "Invalid choice. Exiting..." -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "For more help, see: https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing" -ForegroundColor Blue