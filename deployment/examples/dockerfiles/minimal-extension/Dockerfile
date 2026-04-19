# syntax=docker/dockerfile:1.7
#
# Minimal extension of the Spring Voyage agent base image.
#
# This is the smallest possible custom agent image: take the shipped
# `spring-agent:latest` unchanged and tag it under your own registry /
# namespace. Useful when you want a stable, pinned tag to reference
# from a unit or agent manifest even though no extra tooling is needed.
#
# Build it with:
#   podman build -t localhost/my-agent:latest .
#   # or: docker build -t localhost/my-agent:latest .
#
# Reference it from a unit manifest:
#   unit:
#     name: my-team
#     execution:
#       image: localhost/my-agent:latest
#
# Or through the portal Execution tab (new in the B-wide PR closing
# #601 / #603 / #409) — paste the reference into the Image field.

FROM spring-agent:latest
