#!/bin/bash

docker run \
  --rm \
  -v "$(pwd):/code" \
  minism/plantuml-csharp:latest Assets/Scripts Diagrams

