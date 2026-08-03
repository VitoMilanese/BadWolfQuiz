window.BadWolfWebcam = (() => {
    const peerConfiguration = {
        iceServers: [{ urls: "stun:stun.l.google.com:19302" }]
    };

    const createPlayer = ({
        connection,
        gameCode,
        preview,
        avatar,
        button,
        error,
        enableLabel,
        disableLabel
    }) => {
        let stream = null;
        let peer = null;
        let requested = false;
        let acquiring = false;
        let pageIsActive = true;
        const pendingCandidates = [];

        const showError = message => {
            if (!error) {
                return;
            }

            error.textContent = message;
            error.hidden = false;
        };

        const closePeer = () => {
            peer?.close();
            peer = null;
            pendingCandidates.length = 0;
        };

        const createPeer = () => {
            closePeer();
            peer = new RTCPeerConnection(peerConfiguration);
            for (const track of stream.getTracks()) {
                peer.addTrack(track, stream);
            }
            peer.addEventListener("icecandidate", event => {
                if (event.candidate) {
                    connection.invoke(
                        "SendPlayerWebcamIceCandidate",
                        gameCode,
                        event.candidate.toJSON()).catch(console.error);
                }
            });
            return peer;
        };

        const negotiate = async () => {
            if (!stream || connection.state !== signalR.HubConnectionState.Connected) {
                return;
            }

            const currentPeer = createPeer();
            const offer = await currentPeer.createOffer();
            await currentPeer.setLocalDescription(offer);
            await connection.invoke(
                "SendPlayerWebcamOffer",
                gameCode,
                currentPeer.localDescription.toJSON());
        };

        const disable = async () => {
            requested = false;
            closePeer();
            stream?.getTracks().forEach(track => track.stop());
            stream = null;
            if (preview) {
                preview.srcObject = null;
                preview.hidden = true;
            }
            if (avatar?.getAttribute("src")) {
                avatar.hidden = false;
            }
            if (button) {
                button.textContent = enableLabel;
                button.dataset.webcamEnabled = "false";
            }
            if (connection.state === signalR.HubConnectionState.Connected) {
                await connection.invoke("SetPlayerWebcamEnabled", false);
            }
        };

        const enable = async () => {
            requested = true;
            if (!navigator.mediaDevices?.getUserMedia) {
                showError(error?.dataset.unsupportedLabel || "Camera access requires HTTPS.");
                return;
            }

            if (acquiring || stream) {
                return;
            }

            acquiring = true;
            try {
                const capturedStream = await navigator.mediaDevices.getUserMedia({
                    video: { facingMode: "user" },
                    audio: false
                });
                if (!requested) {
                    capturedStream.getTracks().forEach(track => track.stop());
                    return;
                }
                stream = capturedStream;
                stream.getTracks().forEach(track => {
                    track.enabled = pageIsActive;
                    track.addEventListener("ended", async () => {
                        if (stream !== capturedStream) {
                            return;
                        }
                        closePeer();
                        stream = null;
                        if (preview) {
                            preview.srcObject = null;
                            preview.hidden = true;
                        }
                        if (avatar?.getAttribute("src")) {
                            avatar.hidden = false;
                        }
                        if (connection.state === signalR.HubConnectionState.Connected) {
                            await connection.invoke("SetPlayerWebcamEnabled", false);
                        }
                    }, { once: true });
                });
                if (preview) {
                    preview.srcObject = stream;
                    preview.hidden = !pageIsActive;
                }
                if (avatar) {
                    avatar.hidden = pageIsActive;
                }
                if (error) {
                    error.hidden = true;
                }
                if (button) {
                    button.textContent = disableLabel;
                    button.dataset.webcamEnabled = "true";
                }
                await connection.invoke("SetPlayerWebcamEnabled", pageIsActive);
                if (pageIsActive) {
                    await negotiate();
                }
            } catch (exception) {
                console.error(exception);
                showError(error?.dataset.deniedLabel || "Unable to access the camera.");
                await disable();
            } finally {
                acquiring = false;
            }
        };

        button?.addEventListener("click", () => {
            const action = requested ? disable() : enable();
            action.catch(console.error);
        });

        navigator.mediaDevices?.addEventListener("devicechange", () => {
            if (requested && !stream) {
                enable().catch(console.error);
            }
        });

        connection.on("HostWebcamReady", () => negotiate().catch(console.error));
        connection.on("HostWebcamAnswer", async description => {
            if (!peer) {
                return;
            }
            await peer.setRemoteDescription(description);
            while (pendingCandidates.length > 0) {
                await peer.addIceCandidate(pendingCandidates.shift());
            }
        });
        connection.on("HostWebcamIceCandidate", async candidate => {
            if (!peer) {
                return;
            }
            if (!peer.remoteDescription) {
                pendingCandidates.push(candidate);
                return;
            }
            await peer.addIceCandidate(candidate);
        });

        window.addEventListener("pagehide", () => {
            requested = false;
            stream?.getTracks().forEach(track => track.stop());
            closePeer();
        });

        return {
            disable,
            reconnect: async () => {
                if (stream) {
                    await connection.invoke("SetPlayerWebcamEnabled", true);
                    await negotiate();
                }
            },
            setActive: async isActive => {
                pageIsActive = Boolean(isActive);
                if (!stream || connection.state !== signalR.HubConnectionState.Connected) {
                    return;
                }
                stream.getTracks().forEach(track => track.enabled = isActive);
                if (preview) {
                    preview.hidden = !isActive;
                }
                if (avatar?.getAttribute("src")) {
                    avatar.hidden = isActive;
                }
                await connection.invoke("SetPlayerWebcamEnabled", isActive);
                if (isActive) {
                    await negotiate();
                } else {
                    closePeer();
                }
            }
        };
    };

    const createHost = ({ connection, gameCode, playerList }) => {
        const peers = new Map();
        const streams = new Map();

        const showFallback = playerId => {
            const card = playerList?.querySelector(
                `[data-player-id="${CSS.escape(playerId)}"]`);
            const video = card?.querySelector("[data-player-webcam]");
            const fallback = card?.querySelector("[data-player-webcam-fallback]");
            if (video) {
                video.srcObject = null;
                video.hidden = true;
            }
            if (fallback) {
                fallback.hidden = false;
            }
        };

        const attachStream = playerId => {
            const video = playerList?.querySelector(
                `[data-player-id="${CSS.escape(playerId)}"] [data-player-webcam]`);
            const stream = streams.get(playerId);
            if (video && stream) {
                if (video.srcObject !== stream) {
                    video.srcObject = stream;
                }
                video.hidden = false;
                const fallback = video.closest("[data-player-id]")
                    ?.querySelector("[data-player-webcam-fallback]");
                if (fallback) {
                    fallback.hidden = true;
                }
            }
        };

        const closePeer = connectionId => {
            peers.get(connectionId)?.peer.close();
            peers.delete(connectionId);
        };

        connection.on("PlayerWebcamOffer", async update => {
            closePeer(update.playerConnectionId);
            const peer = new RTCPeerConnection(peerConfiguration);
            const state = {
                peer,
                playerId: update.playerId,
                pendingCandidates: []
            };
            peers.set(update.playerConnectionId, state);

            peer.addEventListener("track", event => {
                const remoteStream = event.streams[0];
                if (remoteStream) {
                    streams.set(update.playerId, remoteStream);
                    attachStream(update.playerId);
                    event.track.addEventListener("ended", () => {
                        streams.delete(update.playerId);
                        showFallback(update.playerId);
                    }, { once: true });
                }
            });
            peer.addEventListener("connectionstatechange", () => {
                if (["failed", "closed"].includes(peer.connectionState)) {
                    streams.delete(update.playerId);
                    showFallback(update.playerId);
                }
            });
            peer.addEventListener("icecandidate", event => {
                if (event.candidate) {
                    connection.invoke(
                        "SendHostWebcamIceCandidate",
                        update.playerConnectionId,
                        event.candidate.toJSON()).catch(console.error);
                }
            });

            await peer.setRemoteDescription(update.sessionDescription);
            while (state.pendingCandidates.length > 0) {
                await peer.addIceCandidate(state.pendingCandidates.shift());
            }
            const answer = await peer.createAnswer();
            await peer.setLocalDescription(answer);
            await connection.invoke(
                "SendHostWebcamAnswer",
                update.playerConnectionId,
                peer.localDescription.toJSON());
        });

        connection.on("PlayerWebcamIceCandidate", async update => {
            const state = peers.get(update.playerConnectionId);
            if (!state) {
                return;
            }
            if (!state.peer.remoteDescription) {
                state.pendingCandidates.push(update.candidate);
                return;
            }
            await state.peer.addIceCandidate(update.candidate);
        });

        window.addEventListener("pagehide", () => {
            for (const state of peers.values()) {
                state.peer.close();
            }
        });

        return {
            register: () => connection.invoke("RegisterHostSession", gameCode),
            refresh: players => {
                const activeWebcams = new Set(
                    players.filter(player => player.webcamEnabled)
                        .map(player => player.id));
                for (const [playerId] of streams) {
                    if (!activeWebcams.has(playerId)) {
                        streams.delete(playerId);
                        showFallback(playerId);
                    }
                }
                for (const playerId of activeWebcams) {
                    attachStream(playerId);
                }
            }
        };
    };

    return { createPlayer, createHost };
})();
