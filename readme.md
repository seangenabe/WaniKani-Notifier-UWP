# WaniKani Notifier UWP

WaniKani new lessons and reviews notifier (and more!) for Universal Windows Platform.

## Features

* Notifies when there are new lessons and reviews available via toast, tile, and badge.¹
* Shows current level radical and kanji progress on tile.²
* Shows critical items (Wall of Shame) on tile.²
* Choose to drill lessons and reviews either in-app or in the default browser.

1. Almost instant notifications via code ported from [wanikani-emitter](https://github.com/seangenabe/wanikani-emitter).
2. Updated daily.

Both update techniques are designed/selected to minimize WaniKani API usage.

## Rationale

(Why should I use *this* instead of the [node.js version](https://github.com/seangenabe/wanikani-notifier) if I'm using a UWP-supported platform like Windows 10?)

* `node-notifier` toast notifications stack. `node-notifier` currently doesn't provide the option to replace existing notifications (it probably wouldn't anyway).
* It's cool to have a dedicated tile.

## node.js version

* [wanikani-notifier](https://github.com/seangenabe/wanikani-notifier)
* [wanikani-emitter](https://github.com/seangenabe/wanikani-emitter)
