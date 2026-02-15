# Scheduled Broadcast Feature Documentation

## Overview

The scheduled broadcast feature allows you to schedule broadcasts for automatic sending at a future time. This is implemented using a background service that runs continuously and checks for scheduled broadcasts every minute.

## Components

### 1. Background Service: `BroadcastSchedulerService`
- **Location**: `Services\BroadcastSchedulerService.cs`
- **Function**: Runs every minute to check for broadcasts with `Status = Scheduled` and `ScheduledSendAt <= Current Time`
- **Registration**: Automatically registered as a hosted service in `Program.cs`

### 2. Enhanced Controller Endpoints
- **Location**: `Controllers\BroadcastController.cs`
- **New Endpoints**:
  - `POST /api/broadcast/schedule` - Schedule a broadcast
  - `POST /api/broadcast/{id}/unschedule` - Cancel scheduling
  - `PUT /api/broadcast/{id}/reschedule` - Change scheduled time
  - `GET /api/broadcast/scheduled` - List all scheduled broadcasts
  - `GET /api/broadcast/scheduler/status` - Get scheduler service status

### 3. DTOs for Scheduling
- **Location**: `DTOs\BroadcastDTOs.cs`
- **New DTOs**:
  - `ScheduleBroadcastRequestDTO`
  - `RescheduleBroadcastRequestDTO`
  - `ScheduledBroadcastInfoDTO`
  - `BroadcastSchedulerStatusDTO`

## API Usage Examples

### Schedule a Broadcast

```http
POST /api/broadcast/schedule
Content-Type: application/json

{
  "broadcastId": 123,
  "scheduledSendAt": "2024-02-15T09:00:00Z"
}
```

**Response**:
```json
{
  "message": "Broadcast scheduled successfully",
  "broadcastId": 123,
  "scheduledSendAt": "2024-02-15T09:00:00Z",
  "status": "Scheduled"
}
```

### Reschedule a Broadcast

```http
PUT /api/broadcast/123/reschedule
Content-Type: application/json

{
  "newScheduledSendAt": "2024-02-15T14:00:00Z"
}
```

### Unschedule a Broadcast

```http
POST /api/broadcast/123/unschedule
```

### Get Scheduled Broadcasts

```http
GET /api/broadcast/scheduled
```

**Response**:
```json
{
  "totalScheduled": 3,
  "broadcasts": [
    {
      "id": 123,
      "title": "Weekly Newsletter",
      "subject": "This Week in Technology",
      "scheduledSendAt": "2024-02-15T09:00:00Z",
  "createdAt": "2024-02-14T15:30:00Z",
      "createdById": "admin",
      "selectedArticlesCount": 5,
  "timeUntilSend": 720.5
  }
  ],
  "currentTime": "2024-02-14T21:00:00Z"
}
```

### Check Scheduler Status

```http
GET /api/broadcast/scheduler/status
```

**Response**:
```json
{
  "schedulerInfo": {
    "isRunning": true,
    "checkIntervalMinutes": 1,
    "lastCheckedAt": "2024-02-14T21:00:00Z",
    "description": "Background service checks for scheduled broadcasts every minute"
  },
  "statistics": {
    "totalScheduledBroadcasts": 3,
    "upcomingInNextHour": 0,
    "upcomingInNext24Hours": 2,
    "overdueBroadcasts": 0
  },
  "currentServerTime": "2024-02-14T21:00:00Z"
}
```

## How It Works

### Workflow
1. **Frontend**: User creates/edits a broadcast and sets `ScheduledSendAt` field
2. **API**: User calls `POST /api/broadcast/schedule` to set status to `Scheduled`
3. **Background Service**: `BroadcastSchedulerService` runs every minute and:
   - Queries for broadcasts where `Status = Scheduled` AND `ScheduledSendAt <= Now`
   - For each found broadcast:
- Temporarily sets status to `Draft` to prevent duplicate processing
     - Calls `IBroadcastSendingService.SendBroadcastAsync()`
     - Sets status to `Sent` if successful, or back to `Scheduled` if failed
4. **Frontend**: Status automatically updates to "Sent" when backend completes sending

### Error Handling
- If sending fails, broadcast status reverts to `Scheduled` for retry
- All errors are logged with broadcast ID and details
- Service continues running even if individual broadcasts fail
- Comprehensive logging for troubleshooting

### Safety Features
- Prevents duplicate sends by temporarily changing status during processing
- Validates that scheduled time is in the future
- Only processes broadcasts with `Scheduled` status
- Graceful error handling with detailed logging

## Testing the Feature

### 1. Create a Test Broadcast
```http
POST /api/broadcast
{
  "title": "Test Scheduled Broadcast",
  "subject": "This is a test",
  "body": "Testing scheduled sending...",
  "channel": "Email",
  "selectedArticleIds": [1, 2]
}
```

### 2. Schedule it for 2 minutes from now
```http
POST /api/broadcast/schedule
{
  "broadcastId": 123,
  "scheduledSendAt": "2024-02-14T21:03:00Z"  // 2 minutes from current time
}
```

### 3. Monitor the status
```http
GET /api/broadcast/123
```

The status should change from `Scheduled` to `Sent` automatically when the scheduled time arrives.

## Monitoring and Maintenance

### Logs to Monitor
- `[BroadcastScheduler]` prefix indicates scheduler activity
- Look for successful sends: "Successfully sent scheduled broadcast"
- Look for errors: "Failed to send scheduled broadcast"

### Key Metrics
- Number of scheduled broadcasts: `GET /api/broadcast/scheduler/status`
- Overdue broadcasts (should be 0 in normal operation)
- Failed broadcasts (should retry automatically)

### Troubleshooting
1. **Broadcasts not sending**: Check logs for `[BroadcastScheduler]` errors
2. **Service not running**: Verify `BroadcastSchedulerService` is registered in `Program.cs`
3. **Time zone issues**: All times should be in UTC (`DateTimeOffset`)
4. **Email delivery issues**: Check email service configuration and member data

## Frontend Integration Notes

The frontend just needs to:
1. Display scheduling options in the broadcast creation/editing UI
2. Call the appropriate API endpoints to schedule/unschedule
3. Show scheduled broadcasts with their status and time remaining
4. The status will automatically update from "Scheduled" to "Sent" - no frontend action required

The backend handles all the automatic sending logic.