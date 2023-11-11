# Minecraft Manager

I host a Java Minecraft server on my computer for my family to play on. 
Often, family members want to stay up playing on the server far later than I do so I leave the server on
for them so they can keep playing. However, that means I have to leave the server - and therefore
my laptop - running all night long, even after they've stopped playing.

Minecraft Manager is a wrapper for my Minecraft server that allows players to issue a command in the chat
that will shutdown the server and shutdown my computer when they're done playing. Safeguards are included
so that the server can only be shutdown by a player when I allow it. Also, there is a countdown so that
shutdown can be aborted.

## Setup

1. Download a release from the [Releases](https://github.com/WestRyanK/MinecraftManager/releases) tab.
2. Create a folder called `manager` in the same directory as your Minecraft `server.jar`.
3. Extract the downloaded zip file into the `manager` folder.
4. Right-click on the MinecraftManager.exe and create a shortcut. Put the shortcut on your desktop.
5. Click your shortcut to open the managed Minecraft server.
6. If you place the Minecraft Manager in a different location, or your server is not called `server.jar`, 
   see the [Command Line Arguments](#command-line-arguments) section for further configuration.

### Command Line Arguments

You can configure Minecraft Manager with command line arguments. You can add arguments to your shortcut
by right-clicking and selecting `Properties` and modifying the value in `Target`.

Here's an example:
```
"C:\path\to\MinecraftManager.exe" ServerPath="C:\path\to\server.jar" IsShutdownEnabled=false ShutdownDelay=60
```

* `ServerPath`: Specify the path to the server.jar file.
* `IsShutdownEnabled`: `true`/`false` whether players can initially issue shutdown commands.
* `ShutdownDelay`: How long to wait (in seconds) before initiating server and computer shutdown.

## How to Use

Start the Minecraft Manager by clicking the shortcut with command line arguments. This will start
up the Minecraft Manager and Minecraft server.

### Server Commands

You can enter any normal server operator commands in the window that appears. In addition, you can
enter the following commands:

* `enable shutdown`: Overrides the command line argument value, allowing users to issue the
  `!shutdown` command in the game chat.
* `disable shutdown`: Overrides the command line argument value, preventing users from issuing 
  the `!shutdown` command in the game chat.

### Game Chat Commands

Users can issue the following commands in the Minecraft chat to control server shutdown. 

**Note**: These commands are not preceded by `/` like normal server commands.

* `!shutdown`: Starts the shutdown countdown. Once the countdown reaches zero, the server and computer start shutting down.
* `!cancel`: Aborts the shutdown countdown. Must be entered in the chat before the countdown reaches zero.
