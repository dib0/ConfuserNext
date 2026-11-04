ConfuserNext
========
ConfuserNext is an open-source protector for .NET applications.
It is a modernized continuation of [ConfuserEx](https://github.com/yck1509/ConfuserEx), which was
itself the successor of the original [Confuser](http://confuser.codeplex.com) project.

NOTICE
======
ConfuserEx was discontinued and unmaintained. ConfuserNext picks the project back up and brings it
to current .NET tooling (SDK-style projects, .NET Framework 4.8, .NET 8 and .NET 10), starting with
this rename. Other forked projects can be found in [this issue](https://github.com/yck1509/ConfuserEx/issues/671).

Features
--------
* Supports .NET Framework 4.8, .NET 8.0 and .NET 10.0
* Symbol renaming (Support WPF/BAML)
* Protection against debuggers/profilers
* Protection against memory dumping
* Protection against tampering (method encryption)
* Control flow obfuscation
* Constant/resources encryption
* Reference hiding proxies
* Disable decompilers
* Embedding dependency
* Compressing output
* Extensible plugin API

Usage
-----
`ConfuserNext.CLI <path to project file>`

The project file is a ConfuserNext Project (*.crproj).
The format of project file can be found in docs\ProjectFormat.md

License
-------
See LICENSE file for details.

Credits
-------
**[0xd4d](https://github.com/yck1509)** for the original work!  
