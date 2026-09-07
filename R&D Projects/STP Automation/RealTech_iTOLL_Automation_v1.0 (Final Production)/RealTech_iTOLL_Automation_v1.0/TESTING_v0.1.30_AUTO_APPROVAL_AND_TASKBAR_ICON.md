# v0.1.30 Test Checklist

## Taskbar icon

1. Publish a fresh standalone build with `build-standalone-exe.bat`.
2. Start `RealTechiTOLLAutomation.exe`.
3. Verify the RealTech iTOLL Automation logo appears on the running taskbar button and Alt+Tab preview.
4. Open Admin Controls, Hardware Settings, and Exceptional Approval; verify each window uses the same icon.
5. If Windows shows an old pinned icon, unpin the old shortcut, close the application, start the new EXE, and pin it again.

## IN missing OUT policy

- Auto IN OFF + Exceptional IN ON: repeated IN opens the exceptional approval popup.
- Auto IN OFF + Exceptional IN OFF: repeated IN is rejected.
- Auto IN ON: repeated IN skips the popup, creates a reconciliation OUT, creates the current IN, debits according to the normal IN rule, and opens the barrier.

## OUT missing IN policy

- Auto OUT OFF + Exceptional OUT ON: OUT without an active IN opens the exceptional approval popup.
- Auto OUT OFF + Exceptional OUT OFF: OUT without an active IN is rejected.
- Auto OUT ON: OUT without an active IN skips the popup, creates a reconciliation IN, creates the current OUT, and opens the barrier.

## Persistence and audit

1. Save all four approval settings, close Admin Controls, and reopen it. Verify the values persist.
2. Restart the application and verify the values remain.
3. Confirm auto-approved trip rows contain:
   - exceptional approval reason,
   - approver name `SYSTEM`,
   - role `IN AUTO APPROVAL` or `OUT AUTO APPROVAL`,
   - mobile `N/A`,
   - approval timestamp.
4. Confirm both reconciliation and current trip records are queued for server sync.
