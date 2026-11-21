# SBOM and supply chain (W45)

## What is asked

W45 tasks 1-3 (tasks 4 and 5 were optional):

1. Generate an SBOM of the container image.
2. Scan the SBOM or image for vulnerabilities.
3. Generate the SBOM in the CI/CD pipeline with GitHub Actions.

The CRA also requires manufacturers to have an SBOM for their product.

## How it works

An **SBOM (software bill of materials)** is the ingredients list of the software: every package in the image, with
its version. Ours lists both our NuGet packages and the Ubuntu packages from the .NET base image, about 3,800
components. The format is **CycloneDX** (JSON).

With the SBOM, a scanner can compare every component against vulnerability databases. When a new vulnerability is
published, the SBOM tells you right away whether you are affected.

In the pipeline, the order is: build the image, make the SBOM, scan it, and only push the image if the scan passes.

## Walk through the code

**.github/workflows/ci.yml**, job `build-and-push`:

- Lines 80-85: build the image and load it locally as `vulntracker:ci`, without pushing yet.
- Lines 87-93: Syft (`anchore/sbom-action`) creates `sbom.cdx.json` and uploads it as a build artifact.
- Lines 96-103: Grype (`anchore/scan-action`) scans the SBOM. It fails on high or critical vulnerabilities that have
  a fix available (`only-fixed`). Vulnerabilities with no fix yet don't block, because we couldn't do anything about them.
- Lines 105-123: only now, log in to GitHub Container Registry and push the image, tagged with the commit sha and `latest`.

Related supply chain measures elsewhere:

- SCA of NuGet packages in the `test` job (file 09).
- The Dockerfile builds inside a container, and the final image has only the runtime (file 01).
- Versions are pinned: Keycloak `26.8.1`, Semgrep `1.183.0`, ZAP `2.17.0`, Postgres `18`.

## How it's tested

Run locally before adding to CI: Syft found 3,775 components in the image, and Grype found no fixable high or critical
vulnerabilities. In CI, the job fails if that changes.

## Try it yourself

Build the image, then generate and scan an SBOM:

```sh
docker compose build app
docker save ssd-app -o app.tar
docker run --rm -v "$PWD:/work" anchore/syft docker-archive:/work/app.tar -o cyclonedx-json=/work/sbom.cdx.json
docker run --rm -v "$PWD:/work" anchore/grype sbom:/work/sbom.cdx.json --only-fixed
```

(The image name depends on the folder name. Check with `docker images`.)

## Questions you might get

**What is a transitive dependency?** A dependency of a dependency. We use Npgsql, which uses other packages. Most of
our packages are transitive, and they can have vulnerabilities too.

**Why scan the SBOM and not just the code?** The image also contains the operating system packages from the base image.
OpenSSL, for example, is not in our code but is in our image.

**How often should you scan?** On every build, and also regularly without a build, because new vulnerabilities are found
in old versions all the time. Our pipeline only scans on build. A scheduled scan would be the next step.

**What is typosquatting?** Publishing a malicious package with a name close to a popular one (`Npgsq1` instead of
`Npgsql`). Copy package names from official documentation and check the download count.

**What would signing the image with cosign add (the optional task)?** Proof that the image in the registry was built by
our pipeline and hasn't been changed since.
