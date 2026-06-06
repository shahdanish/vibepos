CLIENT PROFILES
===============
Each subfolder here is one client installation.

Folder name    = used as the installer filename slug
config.json    = client name (shown in appsettings.json)
firebase-credentials.json = their Firebase service account key (KEEP SECRET)

Example structure:
  clients\
    ABC-Super-Store\
      config.json
      firebase-credentials.json
    XYZ-General-Shop\
      config.json
      firebase-credentials.json

To add a new client, run Build-Installer.cmd and choose "N = New client".
firebase-credentials.json files are excluded from git (see .gitignore).
