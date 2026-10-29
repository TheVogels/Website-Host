## Overview
This program hosts a basic web server which will provide files to the web browser, and can generate a file listing page under /BrowseAll.

## Basics
You will need a folder that usually contains HTML files for the browser to parse, and have a Config.cfg file in the same directory as the website server executable to configure the web server to load files from that path.
Other than that, it is pretty basic.

To connect to the website, you will need to go to http://localhost:PORT/ in your web browser.
To allow others to connect to your website, make a port forward and give them your... *GULP* IP Address.

## Basic Config file.
The supported config options are listed if the file exists, and you can use ```//``` to have a comment in the config file.

What you at minimum need to specify is:
```
// Directory for the website files.
SourcePath: (PATH TO THE FOLDER THIS PROGRAM WILL SERVER TO BROWSERS WILL GO HERE)
// Website Port
port: 900
```

## Keyboard Controls
R - Reload config file.
G - Force a garbage collection.
