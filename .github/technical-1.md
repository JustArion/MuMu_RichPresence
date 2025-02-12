### Technical Document #1

To read the info for the current game we need to read the `MuMuPlayerGlobal-12.0\vms\MuMuPlayerGlobal-12.0-0\logs\shell.log`.<br>
The service log contains the necessary info to provide a rich-presence for Discord.
It contains:
- Startup Timestamp
- Package Name (For Icon / Art)
- Game Title

We can do some processing on our side to get an image via the `PackageName`<br>
To do this we simply do a `GET` request to `https://play.google.com/store/apps/details?id=<PackageName>` and then extract the `<meta property="og:image" content="PACKAGE_IMAGE_LINK">` tag from the head tag

---

**Below are some extracts from the relevant logs.**

Rich Presence:

```
[00:45:31.970 13508/11972][info][:] [Gateway] onAppLaunch: package=com.SmokymonkeyS.Triglav code= msg=
```

Exiting:

```
[00:52:32.304 13508/11972][info][:] [ShellWindow::onTabClose]: index: 1, app info: [id:task:22, packageName:com.SmokymonkeyS.Triglav, appName:Triglav, originName:Triglav, displayId:7]
```

```
[00:45:31.968 13508/13680][info][:] OnFocusOnApp Called: id=task:22, packageName=com.SmokymonkeyS.Triglav, appName=Triglav
```