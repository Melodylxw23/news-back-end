@echo off
echo === Database Migration Conflict Fix ===
echo.
echo This will DELETE your database and recreate it from migrations
echo WARNING: ALL DATA WILL BE LOST!
echo.
set /p confirm="Type 'YES' to continue or press Enter to cancel: "

if /i "%confirm%"=="YES" (
 echo.
    echo Dropping database...
    dotnet ef database drop --force
    echo.
    echo Database dropped successfully!
    echo Restart your application to recreate the database with proper migrations.
    echo.
) else (
    echo Operation cancelled.
)

pause