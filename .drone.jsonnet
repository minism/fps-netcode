local _unity_version = '2019.4.9f1';

local _cache_step(name, restore) = {
  name: name,
  image: 'meltwater/drone-cache:dev',
  pull: true,
  settings: {
    backend: 'filesystem',
    restore: restore,
    rebuild: !restore,
    cache_key: '{{ .Repo.Name }}-library',
    archive_format: 'gzip',
    mount: [
      'Library',
    ],
  },
  volumes: [
    {
      name: 'cache',
      path: '/tmp/cache',
    },
  ],
};

local _build_step(name, target, image_suffix) = {
  name: name,
  image: 'gableroux/unity3d:%s-%s' % [_unity_version, image_suffix],
  environment: {
    BUILD_TARGET: target,
    UNITY_LICENSE_CONTENT: {
      from_secret: 'unity_license',
    },
  },
  commands: [
    'chmod +x ./ci/before_script.sh && ./ci/before_script.sh',
    'chmod +x ./ci/build.sh && ./ci/build.sh',
  ],
};

local _cache_and_build_steps(name, target, image_suffix) = [
  _cache_step('restore %s cache' % name, true),
  _build_step('build %s' % name, target, image_suffix),
  _cache_step('save %s cache' % name, false),
];

local _copy_artifacts_step() = {
  name: 'copy_artifacts',
  image: 'appleboy/drone-scp',
  settings: {
    host: [
      'minornine.com',
    ],
    user: 'm9_builds',
    key: {
      from_secret: 'builds_ssh_key',
    },
    port: 22,
    command_timeout: '2m',
    target: '/home/m9_builds/builds.minornine.com/builds',
    source: [
      'Builds/*.tar.gz',
    ],
    strip_components: 1,
  },
};

{
  kind: 'pipeline',
  type: 'docker',
  name: 'default',
  environment: {
    BUILD_NAME: 'EmptyProject',
    UNITY_ACTIVATION_FILE: './unity3d.alf',
    UNITY_VERSION: _unity_version,
  },
  steps:
    _cache_and_build_steps('osx', 'StandaloneOSX', 'mac') +
    _cache_and_build_steps('win64', 'StandaloneWindows64', 'windows') +
    [_copy_artifacts_step()],
  trigger: {
    event: [
      'tag',
    ],
  },
  volumes: [
    {
      name: 'cache',
      host: {
        path: '/var/lib/drone-cache',
      },
    },
  ],
}
