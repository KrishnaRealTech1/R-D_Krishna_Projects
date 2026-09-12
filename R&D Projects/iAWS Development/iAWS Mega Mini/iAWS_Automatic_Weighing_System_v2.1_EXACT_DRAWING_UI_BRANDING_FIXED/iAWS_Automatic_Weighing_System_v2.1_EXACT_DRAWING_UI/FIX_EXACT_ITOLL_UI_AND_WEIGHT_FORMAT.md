# Exact iTOLL UI sizing + weighbridge format fix

## UI sizing

The main iAWS window now uses the same primary sizing values as the production iTOLL MainWindow:

- Width: 1280
- Height: 800
- MinWidth: 1024
- MinHeight: 720
- WindowState: Maximized
- ResizeMode: CanResize
- Top menu: 25 px
- Right dashboard rail: 330 px
- Bottom log row: 190 px (minimum 160)
- Main work row minimum: 470 px
- Two camera rows minimum: 220 px each

This intentionally replaces the previous 1366 x 768 / 150 px bottom-row layout so the iAWS screen follows iTOLL proportions.

## Weighbridge serial format

The parser now directly supports the supplied indicator format:

```text
wn000000 kg
wn009011 kg
```

Results:

- `wn000000 kg` -> `0 kg`
- `wn009011 kg` -> `9011 kg`

The parser handles CR/LF-delimited frames and complete `wn...kg` frames received without a reliable line ending.
The default weight regex is:

```text
(?i)\bwn\s*(?<weight>\d{1,9}(?:\.\d+)?)\s*kg\b
```

The default weighbridge port is set to COM12 to match the supplied serial-port sample. It can still be changed from Configuration.

## Existing requirements retained

- Four cameras: front, back, left, right
- One weighbridge
- Weight-triggered process
- Direct REST API only
- No payment integration
- No MQTT / FTP / SFTP transaction workflow
- iAWS branding
