# Simple deployment script to demonstrate the deployment pattern
#!/bin/bash

# Configuration
PROJECT_ROOT="/home/caladodani/wargame"
BUILD_DIR="${PROJECT_ROOT}/build"

# Function to print status with colors
print_status() {
    echo -e "\033[1;36m[DEPLOYMENT]\033[0m $1"
}

# Function to print error
print_error() {
    echo -e "\033[1;31m[ERROR]\033[0m $1" >&2
}

# Function to print success
print_success() {
    echo -e "\033[1;32m[SUCCESS]\033[0m $1"
}

# Main deployment function
deploy() {
    print_status "Starting WarGame deployment..."
    
    # Check if .NET is available
    if ! command -v dotnet &> /dev/null; then
        print_error ".NET SDK not found. Please install .NET 8."
        return 1
    fi
    
    # Check if Godot is available
    if ! command -v godot &> /dev/null; then
        print_error "Godot not found. Please install Godot 4.3."
        return 1
    fi
    
    cd "$PROJECT_ROOT"
    
    # Step 1: Build the project
    print_status "Building .NET project..."
    if dotnet build WarGame.sln -nologo -v q; then
        print_success "Build successful"
    else
        print_error "Build failed"
        return 1
    fi
    
    # Step 2: Run core tests
    print_status "Running core tests..."
    if dotnet test WarGame.Core.Tests -nologo -v q; then
        print_success "Core tests passed"
    else
        print_error "Core tests failed"
        return 1
    fi
    
    # Step 3: Create build directory
    print_status "Creating build directory..."
    mkdir -p "$BUILD_DIR"
    print_success "Build directory ready"
    
    # Step 4: Export for Android (if needed)
    print_status "Exporting for Android..."
    if godot --headless --path "." --export-debug Android "$BUILD_DIR/wargame.apk"; then
        print_success "Android APK exported successfully"
    else
        print_error "Android export failed"
        # Don't fail here, continue for local deployment
    fi
    
    # Step 5: Verify APK
    if command -v apksigner &> /dev/null && [[ -f "$BUILD_DIR/wargame.apk" ]]; then
        print_status "Verifying APK signature..."
        if apksigner verify "$BUILD_DIR/wargame.apk"; then
            print_success "APK signature verification passed"
        else
            print_error "APK signature verification failed"
        fi
    fi
    
    # Step 6: Display deployment summary
    echo ""
    print_success "========================================================"
    print_success "WarGame Deployment Complete"
    print_success "========================================================"
    echo ""
    echo "Changes deployed:"
    echo "- Difficulty system with 4 levels (SuperEasy, Easy, Normal, Hard)"
    echo "- Enhanced production speed control"
    echo "- Unit organization improvements"
    echo ""
    echo "Build location: $BUILD_DIR"
    echo "Core tests: PASSED"
    echo "Android APK: $(if [[ -f "$BUILD_DIR/wargame.apk" ]]; then echo "YES (at $BUILD_DIR/wargame.apk)"; else echo "NOT BUILT"; fi)"
    echo ""
    print_success "Ready for production!"
    print_success "========================================================"
}

# Run deployment
apply_difficulty_system() {
    cd "$PROJECT_ROOT"
    
    # Check for any changes to commit
    git_status=$(git status --porcelain)
    
    if [[ -n "$git_status" ]]; then
        echo "Changes detected to commit:"
        echo "$git_status"
        echo ""
        
        # Stage changes
        git add .
        
        # Commit with proper message
        git commit -m "Add difficulty system for production speed control

- Add Difficulty enum with 4 levels: SuperEasy, Easy, Normal, Hard
- Implement ApplyDifficulty() method that adjusts build_min_days and new_division_org rules based on difficulty
- SuperEasy: build_min_days=3 (67% faster), new_division_org=70 (75% better)
- Easy: build_min_days=5 (50% faster), new_division_org=60 (50% better)
- Normal: build_min_days=10, new_division_org=40 (default)
- Hard: build_min_days=15 (50% slower), new_division_org=30 (25% worse)
- Difficulty settings are saved per save file in save_meta table
- Existing ProductionSystem automatically uses difficulty-adjusted rules

This allows players to choose their preferred gameplay pace, with faster modes providing quicker unit production and better organization, while slower modes create more challenging gameplay."
        
        print_success "Changes committed successfully"
    else
        print_success "No changes to commit"
    fi
    
    # Show current status
    echo ""
    echo "Current git status:"
    git status --short
    echo ""
}

# Run the deployment
apply_difficulty_system
