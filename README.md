# Network Engineer Simulator
## written with Artificial Intelligence

This repository contains the core **C# gameplay and networking scripts** for a Unity-based network simulator inspired by **Cisco Packet Tracer**, with added emphasis on **physical realism** (racks, cabling, power) and **interactive device management**.

Assets, scenes, models, and prefabs are intentionally excluded — this repository focuses on **logic and systems**.

---

## Project Goals

- Simulate **real Cisco-style networking behavior** (L2, L3, WAN, STP, etc.)
- Combine **physical datacenter concepts** (racks, PDUs, cabling) with CLI configuration
- Allow hands-on learning through **interactive devices**, not abstract menus
- Keep systems modular so future gameplay layers (labs, objectives, scoring) can be added later

---

## Implemented Features

### Networking & Protocols
- VLANs (access and trunk)
- Inter-VLAN routing (router-on-a-stick)
- Spanning Tree Protocol (STP)
- EtherChannel (static, LACP active/passive, mismatch handling)
- ARP and MAC address learning
- DHCP server and client
- Static routing
- OSPF (multi-process, networks, passive interfaces)
- Extended ACLs (ICMP, TCP, UDP, port-based)
- Standard ACLs
- NAT (inside/outside bindings, overload)

---

### WAN / Serial Links
- Serial interfaces (DCE/DTE)
- Clock rate enforcement on DCE
- Serial links remain down until:
  - Both sides are `no shutdown`
  - DCE side has a valid `clock rate`
- `show controllers serial` support

---

### CLI System
- Cisco-style IOS interpreter
- User exec, privileged exec, global config, and interface config modes
- Command abbreviations (`sh`, `conf t`, etc.)
- Interface configuration (`ip address`, `shutdown`, etc.)
- `show ip interface brief`
- `show running-config`
- `write memory`, `wr mem`, `copy run start`

---

### Configuration Storage & Persistence (Runtime)
- Devices can load a **base configuration** from a Unity `TextAsset`
- On Play:
  - Base config is executed line-by-line through the CLI
- CLI changes modify **running state**
- `write memory`:
  - Generates a **new configuration file**
  - Never overwrites the original TextAsset
  - Saved to `Application.persistentDataPath`
  - Automatically becomes the preferred config on next Play
- Supports routers and switches
- Devices are tracked using stable **Device IDs**

---

### Physical Devices & Power
- Power-aware devices
- Power state propagation
- Power ports and cabling
- PDU groundwork implemented
- Device power state affects links and protocol state

---

### Racks & Physical Installation
- Network rack system with individual **U-slots**
- Each rack slot contains:
  - MountPoint
  - Placeholder
- Devices declare rack compatibility (U-height)
- Devices auto-populate compatible rack slots
- Slot/device replacement menu

---

### Rack Slot Identification
- Hovering an **empty rack slot** shows:
  - Slot label (e.g. `U04`)
- Hovering a **device installed in a rack** shows:
  - Normal device tooltip (ports/interfaces)
  - Additional rack information:
    - `Rack: U04 - U05`
- Device and rack information are merged intelligently

---

### Interaction System
- Raycast-based interaction system
- Modifier-key driven interactions:
  - **Shift + Right Click** → Toggle device terminal popup
  - **Ctrl + Alt + Right Click** → Open rack slot/device swap menu
- Normal right-click interactions preserved where appropriate

---

### Terminal Popup System
- World-space terminal screen spawns in front of the device
- Uses a configurable **MountPoint**
- Forward offset for physical clearance
- Terminal behavior:
  - Toggle via Shift + Right Click on the device
  - CLI interaction only via right-clicking the terminal itself
- Automatically links to:
  - Router
  - Switch
  - PC

---

## Current Project State

- Core networking behavior is fully functional and testable
- Physical and logical layers are tightly integrated
- No full save/load “game state” system yet (by design)
- Ready for:
  - Lab objectives and validation
  - Failure scenarios (power loss, misconfiguration)
  - Guided learning paths
  - Optional scoring or assessment systems

---

## Notes

- This repository intentionally excludes Unity assets, prefabs, and scenes
- Focus is on correctness, extensibility, and educational value
- Designed for future expansion into structured labs or coursework


