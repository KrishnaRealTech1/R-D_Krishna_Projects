The build scripts place runtime tools here before publish:

- ffmpeg.exe      : rolling buffer / event MP4 creation
- ffprobe.exe     : optional FFmpeg companion
- cloudflared.exe : permanent Cloudflare Tunnel connector

Both ffmpeg.exe and cloudflared.exe are embedded into the published single-file EXE when present at build time, and are also copied to Publish\win-x64\tools for portable deployment.
