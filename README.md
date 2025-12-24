# Network Engineer Simulator — Scripts

This repository contains the core C# scripts for a Unity-based
network simulator inspired by Cisco Packet Tracer.

## Implemented Features
- VLANs, trunks, STP, EtherChannel
- Layer 3 routing (router-on-a-stick)
- ARP
- DHCP client/server
- Extended ACLs (ICMP, TCP/UDP, port-based)
- TCP services (FTP/HTTP via telnet)
- Power-aware devices
- Cisco-style CLI interpreter

## Folder Structure
- CLI/ — IOS-style CLI logic
- Devices/ — Routers, switches, PCs
- Networking/ — L2/L3 forwarding
- Services/ — TCP service hosting
- STP/ — Spanning Tree implementation

> Assets, scenes, and prefabs are intentionally excluded.