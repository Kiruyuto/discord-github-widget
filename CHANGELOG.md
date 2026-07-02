# Changelog

## [0.1.1](https://github.com/Kiruyuto/discord-github-widget/compare/discord-github-widget-0.1.0...discord-github-widget-0.1.1) (2026-07-02)


### Bug Fixes

* Widget refresh identity profile route ([cf956be](https://github.com/Kiruyuto/discord-github-widget/commit/cf956befb1576875ecd7e4f4801bca98c8cca613))


### Chores & Maintenance

* Reference cleanups ([6520d81](https://github.com/Kiruyuto/discord-github-widget/commit/6520d8123ba09f7aa66d0bbc7939997fe1d9076e))

## [0.1.0](https://github.com/Kiruyuto/discord-github-widget/compare/discord-github-widget-0.0.1...discord-github-widget-0.1.0) (2026-06-29)


### Features

* Add `/setup-manual` command ([6fa2f31](https://github.com/Kiruyuto/discord-github-widget/commit/6fa2f312596fe7531431f21bb2ee394f0069a0d5))
* Add `Result Handlers` support for deferred interactions ([#15](https://github.com/Kiruyuto/discord-github-widget/issues/15)) ([19437ef](https://github.com/Kiruyuto/discord-github-widget/commit/19437ef8fceb7a815c8da542573c321beccfacac))
* Add background services ([#11](https://github.com/Kiruyuto/discord-github-widget/issues/11)) ([a75fc9d](https://github.com/Kiruyuto/discord-github-widget/commit/a75fc9dc8c1ce92afce75942252e95098366b6a0))
* Add GitHub OAuth2 instead user-provided username ([#6](https://github.com/Kiruyuto/discord-github-widget/issues/6)) ([8891e9a](https://github.com/Kiruyuto/discord-github-widget/commit/8891e9ae32975bdb2a6f1d82195d170b48e11493))
* Initial MVP ([#1](https://github.com/Kiruyuto/discord-github-widget/issues/1)) ([945dc7b](https://github.com/Kiruyuto/discord-github-widget/commit/945dc7bdbf5adc7d8226b7a028fbb755b3230c40))
* Standardize interaction responses with ComponentsV2 ([#19](https://github.com/Kiruyuto/discord-github-widget/issues/19)) ([988a37c](https://github.com/Kiruyuto/discord-github-widget/commit/988a37c4a07b73890cdddebc7ae4db3671108c75))


### Bug Fixes

* `Dockerfile` and `compose.yaml` project references ([0203996](https://github.com/Kiruyuto/discord-github-widget/commit/0203996dece45ceed129617684dea21a545b99d2))
* User application identity registration ([ef3ecda](https://github.com/Kiruyuto/discord-github-widget/commit/ef3ecda468c75d53356e419f2a6674fa3d16c880))


### CI/CD

* Add release workflow ([#12](https://github.com/Kiruyuto/discord-github-widget/issues/12)) ([4f46235](https://github.com/Kiruyuto/discord-github-widget/commit/4f46235f766b71d5ef524524fc4618e7cf02630f))
* Adjust `release-please` PR title pattern ([c2f856d](https://github.com/Kiruyuto/discord-github-widget/commit/c2f856d18be3f3a76435de2eb8d98b1b48b9a9bd))
* Basic repo config ([#2](https://github.com/Kiruyuto/discord-github-widget/issues/2)) ([b03cc38](https://github.com/Kiruyuto/discord-github-widget/commit/b03cc38859afd733cf183a904dc3f73c3a90aea3))


### Chores & Maintenance

* Align widget flow with documentation ([6fa2f31](https://github.com/Kiruyuto/discord-github-widget/commit/6fa2f312596fe7531431f21bb2ee394f0069a0d5))
* **dependencies:** Pin dependencies ([#13](https://github.com/Kiruyuto/discord-github-widget/issues/13)) ([647a307](https://github.com/Kiruyuto/discord-github-widget/commit/647a3073bb64af90dc51661330e92a2908784fdc))
* **dependencies:** Update .NET non-Major dependencies ([#18](https://github.com/Kiruyuto/discord-github-widget/issues/18)) ([b4bde39](https://github.com/Kiruyuto/discord-github-widget/commit/b4bde39026bd4025dcb38b7de889b5b0e16231cb))
* **dependencies:** Update .NET non-Major dependencies ([#3](https://github.com/Kiruyuto/discord-github-widget/issues/3)) ([ef98362](https://github.com/Kiruyuto/discord-github-widget/commit/ef98362f7f5fbad7ad1dffef74e5d94fa1206568))
* **dependencies:** Update .NET non-Major dependencies ([#7](https://github.com/Kiruyuto/discord-github-widget/issues/7)) ([034d1fd](https://github.com/Kiruyuto/discord-github-widget/commit/034d1fd4f93811d1337977f2a433c73b5712d113))
* **dependencies:** Update actions/setup-dotnet action to v5.4.0 ([#17](https://github.com/Kiruyuto/discord-github-widget/issues/17)) ([85fe909](https://github.com/Kiruyuto/discord-github-widget/commit/85fe909b478e8b3cf5e722cd31f923d4f0722bb2))
* **dependencies:** Update Docker dependencies ([#8](https://github.com/Kiruyuto/discord-github-widget/issues/8)) ([83a046a](https://github.com/Kiruyuto/discord-github-widget/commit/83a046a53ca5c560dca203ada795b181b9426869))
* **dependencies:** Update GitHub Actions dependencies to v7 ([#5](https://github.com/Kiruyuto/discord-github-widget/issues/5)) ([f0731a1](https://github.com/Kiruyuto/discord-github-widget/commit/f0731a1194aed38e9ca05db6eee459deff7f4159))
* Organize projects under `Source` directory ([5d27eac](https://github.com/Kiruyuto/discord-github-widget/commit/5d27eac5550cab63599355f41bed35575c665946))
* Simplify compose behavior as dev sandbox ([a75fc9d](https://github.com/Kiruyuto/discord-github-widget/commit/a75fc9dc8c1ce92afce75942252e95098366b6a0))


### Documentation

* Add setup guide; Adjust README ([#9](https://github.com/Kiruyuto/discord-github-widget/issues/9)) ([6fa2f31](https://github.com/Kiruyuto/discord-github-widget/commit/6fa2f312596fe7531431f21bb2ee394f0069a0d5))
* Polish README and setup guide wording ([#10](https://github.com/Kiruyuto/discord-github-widget/issues/10)) ([bf3fbe5](https://github.com/Kiruyuto/discord-github-widget/commit/bf3fbe5406b986ae264d5918cdaf98ccb4d5ea2b))


### Style

* Improve the content of `/invite` ([33b0584](https://github.com/Kiruyuto/discord-github-widget/commit/33b0584e4a0e9c9a805ca8cb382830ebea256a66))


### Performance

* Reduce unnecessary allocations ([#16](https://github.com/Kiruyuto/discord-github-widget/issues/16)) ([ef3ecda](https://github.com/Kiruyuto/discord-github-widget/commit/ef3ecda468c75d53356e419f2a6674fa3d16c880))
