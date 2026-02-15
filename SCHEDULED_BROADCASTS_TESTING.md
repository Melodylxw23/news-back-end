# Scheduled Broadcast Feature - Testing Guide

## ? Implementation Complete

The scheduled broadcast feature has been successfully implemented with the following components:

### ?? Backend Components Added:

1. **`BroadcastSchedulerService`** - Background service that runs every minute
2. **Enhanced `BroadcastController`** - New endpoints for scheduling management
3. **Scheduling DTOs** - Data transfer objects for API requests/responses
4. **Service Registration** - Automatically starts with the application

### ?? New API Endpoints Available:

| Endpoint | Method | Purpose |
|----------|---------|---------|
| `/api/broadcast/schedule` | POST | Schedule a broadcast for future sending |
| `/api/broadcast/{id}/unschedule` | POST | Cancel a scheduled broadcast |
| `/api/broadcast/{id}/reschedule` | PUT | Change the scheduled time |
| `/api/broadcast/scheduled` | GET | List all scheduled broadcasts |
| `/api/broadcast/scheduler/status` | GET | Check scheduler service status |

### ?? Testing Instructions:

#### Step 1: Create a Broadcast
```http
POST /api/broadcast
Authorization: Bearer <your-jwt-token>
Content-Type: application/json

{
  "title": "Test Scheduled Newsletter",
  "subject": "Your Weekly Update",
  "body": "This is a test of the scheduled broadcast feature!",
  "channel": "Email",
  "targetAudience": "All",
  "selectedArticleIds": []
}
```

#### Step 2: Schedule the Broadcast (2 minutes from now)
```http
POST /api/broadcast/schedule
Authorization: Bearer <your-jwt-token>
Content-Type: application/json

{
  "broadcastId": <ID_FROM_STEP_1>,
  "scheduledSendAt": "<CURRENT_TIME_PLUS_2_MINUTES_IN_UTC>"
}
```

**Example scheduledSendAt**: If current time is `2024-02-14T21:00:00Z`, use `2024-02-14T21:02:00Z`

#### Step 3: Verify Scheduling
```http
GET /api/broadcast/scheduled
Authorization: Bearer <your-jwt-token>
```

You should see your broadcast listed with a status of "Scheduled".

#### Step 4: Monitor Automatic Sending
After 2 minutes, check the broadcast status:

```http
GET /api/broadcast/<BROADCAST_ID>
Authorization: Bearer <your-jwt-token>
```

The status should automatically change from "Scheduled" to "Sent".

#### Step 5: Check Service Status
```http
GET /api/broadcast/scheduler/status
Authorization: Bearer <your-jwt-token>
```

This shows the scheduler is running and provides statistics.

### ?? Monitoring and Logs

Look for these log entries in your console/logs:
- `[BroadcastScheduler] Starting scheduled broadcast service...`
- `[BroadcastScheduler] Found X scheduled broadcasts ready to send`
- `[BroadcastScheduler] Successfully sent scheduled broadcast X`

### ?? Configuration

The background service is automatically configured and starts with your application. No additional configuration needed!

### ?? How It Works

1. **Background Service**: `BroadcastSchedulerService` runs continuously
2. **Every Minute**: Checks for broadcasts where `Status = Scheduled` AND `ScheduledSendAt <= Now`
3. **Automatic Sending**: Uses existing `BroadcastSendingService.SendBroadcastAsync()`
4. **Status Updates**: Changes status to "Sent" when successful
5. **Error Handling**: Logs errors and retries failed broadcasts

### ?? Production Deployment Notes

- The service starts automatically when your app starts
- No database changes required (uses existing `ScheduledSendAt` field)
- Timezone safe (uses UTC `DateTimeOffset`)
- Handles multiple scheduled broadcasts concurrently
- Graceful error handling and retry logic

### ?? Frontend Integration

The frontend can now:
1. Set `scheduledSendAt` when creating/editing broadcasts
2. Call `/api/broadcast/schedule` to activate scheduling
3. Monitor status changes automatically
4. Show time remaining until send via `/api/broadcast/scheduled`

Your backend is now ready for scheduled broadcasts! ??