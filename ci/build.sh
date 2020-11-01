#!/usr/bin/env bash

set -e
set -x

echo "Building for $BUILD_TARGET"

# SRC_PREFIX is a hack for when the unity project root is a nested dir (like "Unity")
export SRC_PREFIX=""
export BUILD_PATH=./Builds/$BUILD_TARGET/
mkdir -p $BUILD_PATH

  # Add this arg if SRC_PREFIX is non-null.
  # -projectPath $SRC_PREFIX \
${UNITY_EXECUTABLE:-xvfb-run --auto-servernum --server-args='-screen 0 640x480x24' /opt/Unity/Editor/Unity} \
  -quit \
  -batchmode \
  -buildTarget $BUILD_TARGET \
  -customBuildTarget $BUILD_TARGET \
  -customBuildName $BUILD_NAME \
  -customBuildPath $BUILD_PATH \
  -executeMethod BuildCommand.PerformBuild \
  -logFile /dev/stdout

UNITY_EXIT_CODE=$?

if [ $UNITY_EXIT_CODE -eq 0 ]; then
  echo "Run succeeded, no failures occurred";
elif [ $UNITY_EXIT_CODE -eq 2 ]; then
  echo "Run succeeded, some tests failed";
elif [ $UNITY_EXIT_CODE -eq 3 ]; then
  echo "Run failure (other failure)";
else
  echo "Unexpected exit code $UNITY_EXIT_CODE";
fi

OUTPUT_DIR=$SRC_PREFIX/$BUILD_PATH

ls -la $OUTPUT_DIR
[ -n "$(ls -A $OUTPUT_DIR)" ] # fail job if build folder is empty

# Create tar.gz of the build.
cd $OUTPUT_DIR
tar czf ../$BUILD_NAME-$BUILD_TARGET-$DRONE_TAG.tar.gz *
