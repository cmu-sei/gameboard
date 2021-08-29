﻿// <auto-generated />
using System;
using Gameboard.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Gameboard.Api.Data.Migrations.PostgreSQL.GameboardDb
{
    [DbContext(typeof(GameboardDbContextPostgreSQL))]
    partial class GameboardDbContextPostgreSQLModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .UseIdentityByDefaultColumns()
                .HasAnnotation("Relational:MaxIdentifierLength", 63)
                .HasAnnotation("ProductVersion", "5.0.0");

            modelBuilder.Entity("Gameboard.Api.Data.Challenge", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<DateTimeOffset>("EndTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ExternalId")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<string>("GameId")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<bool>("HasDeployedGamespace")
                        .HasColumnType("boolean");

                    b.Property<DateTimeOffset>("LastScoreTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("LastSyncTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .HasColumnType("text");

                    b.Property<string>("PlayerId")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<int>("Points")
                        .HasColumnType("integer");

                    b.Property<double>("Score")
                        .HasColumnType("double precision");

                    b.Property<string>("SpecId")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<DateTimeOffset>("StartTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("State")
                        .HasColumnType("text");

                    b.Property<string>("Tag")
                        .HasColumnType("text");

                    b.Property<string>("TeamId")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<DateTimeOffset>("WhenCreated")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("GameId");

                    b.HasIndex("PlayerId");

                    b.HasIndex("TeamId");

                    b.ToTable("Challenges");
                });

            modelBuilder.Entity("Gameboard.Api.Data.ChallengeEvent", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<string>("ChallengeId")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<string>("TeamId")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<string>("Text")
                        .HasMaxLength(1024)
                        .HasColumnType("character varying(1024)");

                    b.Property<DateTimeOffset>("Timestamp")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("Type")
                        .HasColumnType("integer");

                    b.Property<string>("UserId")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.HasKey("Id");

                    b.HasIndex("ChallengeId");

                    b.ToTable("ChallengeEvents");
                });

            modelBuilder.Entity("Gameboard.Api.Data.ChallengeSpec", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<int>("AverageDeploySeconds")
                        .HasColumnType("integer");

                    b.Property<string>("Description")
                        .HasColumnType("text");

                    b.Property<bool>("Disabled")
                        .HasColumnType("boolean");

                    b.Property<string>("ExternalId")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<string>("GameId")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<string>("Name")
                        .HasColumnType("text");

                    b.Property<int>("Points")
                        .HasColumnType("integer");

                    b.Property<float>("R")
                        .HasColumnType("real");

                    b.Property<string>("Tag")
                        .HasColumnType("text");

                    b.Property<float>("X")
                        .HasColumnType("real");

                    b.Property<float>("Y")
                        .HasColumnType("real");

                    b.HasKey("Id");

                    b.HasIndex("GameId");

                    b.ToTable("ChallengeSpecs");
                });

            modelBuilder.Entity("Gameboard.Api.Data.Game", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<bool>("AllowPreview")
                        .HasColumnType("boolean");

                    b.Property<bool>("AllowReset")
                        .HasColumnType("boolean");

                    b.Property<string>("Background")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("CardText1")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("CardText2")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("CardText3")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Competition")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Division")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<DateTimeOffset>("GameEnd")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("GameMarkdown")
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("GameStart")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("GamespaceLimitPerSession")
                        .HasColumnType("integer");

                    b.Property<bool>("IsPublished")
                        .HasColumnType("boolean");

                    b.Property<string>("Key")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Logo")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<int>("MaxAttempts")
                        .HasColumnType("integer");

                    b.Property<int>("MaxTeamSize")
                        .HasColumnType("integer");

                    b.Property<int>("MinTeamSize")
                        .HasColumnType("integer");

                    b.Property<string>("Mode")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<string>("Name")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<DateTimeOffset>("RegistrationClose")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("RegistrationConstraint")
                        .HasMaxLength(1024)
                        .HasColumnType("character varying(1024)");

                    b.Property<string>("RegistrationMarkdown")
                        .HasMaxLength(1024)
                        .HasColumnType("character varying(1024)");

                    b.Property<DateTimeOffset>("RegistrationOpen")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("RegistrationType")
                        .HasColumnType("integer");

                    b.Property<bool>("RequireSponsoredTeam")
                        .HasColumnType("boolean");

                    b.Property<string>("Season")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<int>("SessionLimit")
                        .HasColumnType("integer");

                    b.Property<int>("SessionMinutes")
                        .HasColumnType("integer");

                    b.Property<string>("Sponsor")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<string>("TestCode")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Track")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.HasKey("Id");

                    b.ToTable("Games");
                });

            modelBuilder.Entity("Gameboard.Api.Data.Player", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<string>("ApprovedName")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<int>("CorrectCount")
                        .HasColumnType("integer");

                    b.Property<string>("GameId")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<string>("InviteCode")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<string>("Name")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<int>("PartialCount")
                        .HasColumnType("integer");

                    b.Property<int>("Rank")
                        .HasColumnType("integer");

                    b.Property<int>("Role")
                        .HasColumnType("integer");

                    b.Property<int>("Score")
                        .HasColumnType("integer");

                    b.Property<DateTimeOffset>("SessionBegin")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("SessionEnd")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("SessionMinutes")
                        .HasColumnType("integer");

                    b.Property<string>("Sponsor")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<string>("TeamId")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<long>("Time")
                        .HasColumnType("bigint");

                    b.Property<string>("UserId")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.HasKey("Id");

                    b.HasIndex("GameId");

                    b.HasIndex("TeamId");

                    b.HasIndex("UserId");

                    b.ToTable("Players");
                });

            modelBuilder.Entity("Gameboard.Api.Data.Sponsor", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<bool>("Approved")
                        .HasColumnType("boolean");

                    b.Property<string>("Logo")
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)");

                    b.HasKey("Id");

                    b.ToTable("Sponsors");
                });

            modelBuilder.Entity("Gameboard.Api.Data.User", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<string>("ApprovedName")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Email")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Name")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<int>("Role")
                        .HasColumnType("integer");

                    b.Property<string>("Sponsor")
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)");

                    b.Property<string>("Username")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("Gameboard.Api.Data.Challenge", b =>
                {
                    b.HasOne("Gameboard.Api.Data.Game", "Game")
                        .WithMany("Challenges")
                        .HasForeignKey("GameId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("Gameboard.Api.Data.Player", "Player")
                        .WithMany("Challenges")
                        .HasForeignKey("PlayerId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.Navigation("Game");

                    b.Navigation("Player");
                });

            modelBuilder.Entity("Gameboard.Api.Data.ChallengeEvent", b =>
                {
                    b.HasOne("Gameboard.Api.Data.Challenge", "Challenge")
                        .WithMany("Events")
                        .HasForeignKey("ChallengeId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.Navigation("Challenge");
                });

            modelBuilder.Entity("Gameboard.Api.Data.ChallengeSpec", b =>
                {
                    b.HasOne("Gameboard.Api.Data.Game", "Game")
                        .WithMany("Specs")
                        .HasForeignKey("GameId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.Navigation("Game");
                });

            modelBuilder.Entity("Gameboard.Api.Data.Player", b =>
                {
                    b.HasOne("Gameboard.Api.Data.Game", "Game")
                        .WithMany("Players")
                        .HasForeignKey("GameId");

                    b.HasOne("Gameboard.Api.Data.User", "User")
                        .WithMany("Enrollments")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.Navigation("Game");

                    b.Navigation("User");
                });

            modelBuilder.Entity("Gameboard.Api.Data.Challenge", b =>
                {
                    b.Navigation("Events");
                });

            modelBuilder.Entity("Gameboard.Api.Data.Game", b =>
                {
                    b.Navigation("Challenges");

                    b.Navigation("Players");

                    b.Navigation("Specs");
                });

            modelBuilder.Entity("Gameboard.Api.Data.Player", b =>
                {
                    b.Navigation("Challenges");
                });

            modelBuilder.Entity("Gameboard.Api.Data.User", b =>
                {
                    b.Navigation("Enrollments");
                });
#pragma warning restore 612, 618
        }
    }
}
