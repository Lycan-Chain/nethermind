#!/bin/bash
# SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
# SPDX-License-Identifier: LGPL-3.0-only

#exit when any command fails
set -e
echo =======================================================
echo Setting up Nethermind packages
echo =======================================================

cd $RELEASE_DIRECTORY
mkdir $LIN_RELEASE
mkdir $OSX_RELEASE
mkdir $WIN_RELEASE
mkdir $LIN_ARM64_RELEASE
mkdir $OSX_ARM64_RELEASE

echo =======================================================
echo Setting up Nethermind packages completed
echo =======================================================
