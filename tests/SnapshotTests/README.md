# Snapshot Tests

These tests have two layers of snapshots. They intercept the calls and response to Google Cloud.
By default the tests use the saved values, so the tests can be run without internet and are not
effected by minor changes in LLM produced text. If you run the `update-snapshot-tests` script
at the root of this repository, the requests will be sent to GCP and the response will be saved.

The second level of snapshotting is done using the
[Verify snapshot testing library](https://github.com/VerifyTests/Verify).
This is used to ensure the values we return from our library have the expected shape,
without having to write a pile of asserts.

The library has a `TestHelpers` class that allows the library to detect when it is running
under a unit test. This allows us to tweak some aspects of the response to make them more
deterministic from run to run.
